using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using Mindscape.AcademicPressure;
using Mindscape.Data;

/// <summary>Reference-inspired menu. The layout, text and styling live in Assets/UI Toolkit/MainMenuNew/MainMenuNew.uxml
/// (+ .uss) and are edited in UI Builder; this finds elements by name and hooks up what they do. Only data-driven
/// parts are filled in here: the Continue save slot, journal text, lock and progress states, the account line,
/// key bindings and the saved option values.</summary>
[ExecuteAlways, RequireComponent(typeof(UIDocument))]
public class MainMenuNewController : MonoBehaviour
{
    [Tooltip("Optional: replaces the menu and About page background set in the UXML.")]
    public Texture2D background;
    [Tooltip("The 3D title (MindScape3D_MainMenu_GlowOnly) shown in the logo spots. Empty: the flat MenuArt/title picture from the UXML.")]
    public TitleModelView titleModel;
    public string tutorialScene = "Tutorial";
    [Tooltip("Entry scene for Chapter 2 · Family Expectations, Episode 1.")]
    public string episodeScene = "Cutscene - Episode 1";
    readonly Dictionary<string, VisualElement> pages = new Dictionary<string, VisualElement>();
    VisualElement root, wiredRoot, veil, stage, brightness;
    string currentPage = "home";
    Label toast;
    InputActionRebindingExtensions.RebindingOperation rebind;
    InputAction rebindingAction;
    bool actionWasEnabled;
    bool loading, signingIn;
    PanelSettings desktopSettings, mobileSettings;
    TextField journal;
    int journalIndex = 101;   // Chapter 1 · Episode 1
    readonly string[] episodeNames = { "The Paper on the Desk", "Before the Bell", "Running Out of Time", "The Weight Behind the Worry", "What I Left Behind" };
    // Lock state follows progression (MindscapeProgress) and is refreshed whenever a chapter page opens.
    readonly List<(Button row, Label state, int chapter, int episode)> episodeRows = new List<(Button, Label, int, int)>();
    readonly List<(Button card, Label bottom, int chapter)> chapterCards = new List<(Button, Label, int)>();
    Label progressCaption; VisualElement progressFill;
    VisualElement journalActions, journalSummary; Label journalSummaryText;
    public string CurrentPage => currentPage;

    void OnEnable() { Rebuild(); if (Application.isPlaying) MindscapeCloud.Changed += OnCloudChanged; }
    public void Rebuild()
    {
        rebind?.Cancel();
        root = GetComponent<UIDocument>().rootVisualElement.Q("menu-root");
        if (root == null) return;
        ApplyMobileMode();
        loading = false;
        // Hook everything up once per tree (a UXML reload makes a new one).
        if (wiredRoot != root) { wiredRoot = root; Bind(); }
        brightness.style.opacity = Mathf.Clamp01(PlayerPrefs.GetFloat("Mindscape.MenuShade", 0)) * .45f;
        RefreshLocks();
        ShowPage("home");
        // Ask to log in (or play offline) once; a stored login or offline choice skips straight to the home page.
        if (Application.isPlaying) { if (NeedsLogin) ShowPage("login"); else if (MindscapeCloud.IsSignedIn && !MindscapeCloud.IsLoaded) RefreshCloud(); }
        if (Application.isPlaying) { GraphicsPrefs.Apply(); Time.timeScale = 1; UnityEngine.Cursor.lockState = CursorLockMode.None; UnityEngine.Cursor.visible = true; }
    }

    void Bind()
    {
        pages.Clear(); episodeRows.Clear(); chapterCards.Clear(); passwordToggles.Clear();
        veil = root.Q("veil"); stage = root.Q("stage"); toast = root.Q<Label>("toast"); brightness = root.Q("brightness");
        if (background != null) { root.style.backgroundImage = background; var art = root.Q("about-art"); if (art != null) art.style.backgroundImage = background; }
        if (Application.isPlaying && titleModel != null)
        {
            titleModel.Build();   // this Awake / OnEnable may run before the model's own
            if (titleModel.Ready) foreach (var logo in root.Query(className: "logo").ToList()) { logo.AddToClassList("logo-3d"); logo.style.backgroundImage = Background.FromRenderTexture(titleModel.Texture); }
            // The home title's soft dark glow is the same animated picture, blurred and darkened (.logo-glow).
            if (titleModel.Ready) foreach (var glow in root.Query(className: "logo-glow").ToList()) glow.style.backgroundImage = Background.FromRenderTexture(titleModel.Texture);
        }
        foreach (var page in stage.Children()) if (page.name.StartsWith("page-")) pages[page.name.Substring(5)] = page;
        foreach (var key in pages.Keys) { string page = key; On("back-" + page, () => ShowPage(page.StartsWith("episodes") ? "chapters" : "home")); }
        BindLogin(); BindHome(); BindChapters(); BindEpisodes(); BindJournal(); BindOptions(); BindGuide(); BindResources(); BindQuit();
        root.RegisterCallback<GeometryChangedEvent>(evt =>
        {
            // A landscape phone is narrow in mobile mode's large logical pixels, but it is not a portrait screen.
            bool isPortrait = root.resolvedStyle.width < root.resolvedStyle.height || (root.resolvedStyle.width < 960 && !root.ClassListContains("mobile"));
            bool isCompactHeight = root.resolvedStyle.height < 680;
            root.EnableInClassList("portrait", isPortrait);
            root.EnableInClassList("compact-height", isCompactHeight);
        });
    }

    void Update()
    {
        if (!Application.isPlaying) return;
        // Editing the UXML in UI Builder during Play reloads the tree; hook the new one up.
        if (root == null || root.panel == null) { Rebuild(); if (root == null) return; }
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && LoginErrorOpen) CloseLoginError();
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && PrivacyOpen) ClosePrivacy(false);
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && rebind == null && accountStep != AccountStep.None) CloseModal();
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && rebind == null && currentPage == "register") ShowPage("login");
        else if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && rebind == null && currentPage != "login")
            ShowPage(currentPage.StartsWith("episodes") ? "chapters" : currentPage == "home" ? "quit" : "home");
    }
    void OnDestroy() { if (mobileSettings != null) Destroy(mobileSettings); }
    // Mobile mode (phones and tablets, or the Mobile controls option): the whole menu is drawn 1.6x larger so every
    // button is a finger-sized target, and the .mobile rules in MainMenuNew.uss fit the pages to the shorter screen.
    // The document gets its own copy of the panel settings, so the shared asset is never changed.
    void ApplyMobileMode()
    {
        bool mobile = Application.isPlaying && MobileControls.Enabled;
        var document = GetComponent<UIDocument>();
        if (desktopSettings == null) desktopSettings = document.panelSettings;
        if (mobile && mobileSettings == null && desktopSettings != null)
        {
            mobileSettings = Instantiate(desktopSettings);
            mobileSettings.name = desktopSettings.name + " (Mobile)";
            mobileSettings.referenceResolution = new Vector2Int(800, 450);
        }
        var wanted = mobile && mobileSettings != null ? mobileSettings : desktopSettings;
        if (wanted != null && document.panelSettings != wanted) document.panelSettings = wanted;
        root?.EnableInClassList("mobile", mobile);
    }
    void OnDisable() { MindscapeCloud.Changed -= OnCloudChanged; if (currentPage == "journal" && journal != null && root != null) LoadJournal(journalIndex); rebind?.Cancel(); rebind?.Dispose(); rebind = null; if (Application.isPlaying) PlayerPrefs.Save(); }

    // ---------------------------------------------------------------- binding helpers
    // A click (with the menu click sound) for the named button; replacing the Clickable means re-binding never doubles it.
    void On(string name, Action click) => On(root.Q<Button>(name), click);
    static void On(Button button, Action click)
    {
        if (button == null) return;
        button.clickable = new Clickable(() => { if (Application.isPlaying) SFXManager.PlayClick(); click?.Invoke(); });
    }
    // For the few pieces still made in code (the Continue save slot).
    static VisualElement Box(VisualElement parent, string classes, string name = null)
    {
        var element = new VisualElement { name = name }; AddClasses(element, classes); parent.Add(element); if (element.ClassListContains("paper")) element.Add(new PaperGrain()); return element;
    }
    static void AddClasses(VisualElement element, string classes) { foreach (var c in classes.Split(' ')) if (c.Length > 0) element.AddToClassList(c); }
    static Label Text(VisualElement parent, string value, string classes = "body")
    { var label = new Label(value); AddClasses(label, classes); parent.Add(label); return label; }
    static Button Button(VisualElement parent, string label, Action click, string classes = "", string name = null)
    {
        var b = new Button { text = label, name = name }; On(b, click);
        AddClasses(b, classes); parent.Add(b); return b;
    }
    // A sidebar of tabs (<page>-tab-N) that shows one panel (<page>-panel-N) at a time.
    void Tabs(string page, int count, Action<int> opened = null)
    {
        for (int i = 0; i < count; i++)
        {
            int index = i;
            On(page + "-tab-" + i, () =>
            {
                rebind?.Cancel();
                for (int j = 0; j < count; j++)
                {
                    root.Q(page + "-tab-" + j)?.EnableInClassList("selected", j == index);
                    root.Q(page + "-panel-" + j)?.EnableInClassList("hidden", j != index);
                }
                opened?.Invoke(index);
            });
        }
    }

    public void ShowPage(string name)
    {
        if (name == "continue" && Application.isPlaying && !ContinueAvailable) name = "chapters";
        if (!pages.ContainsKey(name)) return;
        if (rebind != null) rebind.Cancel();
        if (currentPage == "journal" && name != "journal") LoadJournal(journalIndex);
        currentPage = name;
        HidePasswords();
        foreach (var item in pages) item.Value.EnableInClassList("hidden", item.Key != name);
        veil.EnableInClassList("home-veil", name == "home");
        root.Q("account")?.EnableInClassList("hidden", name != "home");
        if (Application.isPlaying) RefreshHome();
        if (name == "continue") RefreshContinue();
        if (name == "chapters" || name.StartsWith("episodes")) RefreshLocks();
        if (name == "journal") LoadJournal(journalIndex);
        if (toast != null) toast.AddToClassList("hidden");
    }
    void Notify(string message)
    {
        toast.text = message; toast.RemoveFromClassList("hidden");
        toast.schedule.Execute(() => toast.AddToClassList("hidden")).StartingIn(5000);
    }
    // ---------------------------------------------------------------- account
    static bool NeedsLogin => !MindscapeCloud.IsSignedIn && !MindscapeCloud.IsOffline;
    static string AccountLabel => MindscapeCloud.IsSignedIn ? "Signed in as " + MindscapeCloud.Username : MindscapeCloud.IsOffline ? "Playing offline · saved on this device" : "";
    static string AccountAction => MindscapeCloud.IsSignedIn ? "Sign out" : "Log in";
    void BindLogin()
    {
        foreach (var field in new[] { "login-password", "register-password", "modal-identifier", "modal-code", "modal-password2" })
        {
            string name = field;
            root.Q<TextField>(name)?.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
                if (name == "login-password") SignIn(); else if (name == "register-password") StartSignup(); else SubmitModal();
            });
        }
        passwordToggles.Clear();
        foreach (var name in new[] { "login-password", "register-password", "modal-password", "modal-password2" }) BindPasswordToggle(name);
        On("login-offline", PlayOffline);
        On("login-register", () => { SetStatus("register-status", ""); ShowPage("register"); });
        On("login-submit", SignIn);
        On("login-error-close", CloseLoginError);
        On("login-error-reset", () => { CloseLoginError(); OpenModal(AccountStep.ResetStart); });
        On("login-forgot", () => OpenModal(AccountStep.ResetStart));
        On("login-recover", () => OpenModal(AccountStep.RecoverStart));
        On("login-google", StartGoogle);
        On("register-back", () => ShowPage("login"));
        On("register-submit", StartSignup);
        BindSignupChecks();
        On("modal-cancel", CloseModal);
        On("modal-resend", ResendCode);
        On("modal-submit", SubmitModal);
        var spinner = root.Q("login-spinner");
        spinner?.schedule.Execute(() =>
        {
            if (signingIn) spinner.style.rotate = new Rotate(new Angle((Time.unscaledTime * 360f) % 360f, AngleUnit.Degree));
        }).Every(16);
        SetLoginLoading(signingIn);
        BindConsent();
    }
    // Registration consent: Send code stays disabled until the player reads the Data Privacy Notice and agrees.
    // Clicking the box always opens the notice; "I agree" ticks it, "I do not agree" (or Esc) leaves it unticked.
    bool privacyAccepted;
    void BindConsent()
    {
        var consent = root.Q<Toggle>("register-consent");
        consent?.RegisterValueChangedCallback(evt => { consent.SetValueWithoutNotify(privacyAccepted); OpenPrivacy(); });
        On("privacy-accept", () => ClosePrivacy(true));
        On("privacy-decline", () => ClosePrivacy(false));
        SetConsent(false);
    }
    bool PrivacyOpen => root.Q("privacy-modal") is VisualElement modal && !modal.ClassListContains("hidden");
    void OpenPrivacy()
    {
        var modal = root.Q("privacy-modal"); if (modal == null) return;
        var notice = modal.Q<ScrollView>(); if (notice != null) notice.scrollOffset = Vector2.zero;
        modal.RemoveFromClassList("hidden");
    }
    void ClosePrivacy(bool accepted) { root.Q("privacy-modal")?.AddToClassList("hidden"); SetConsent(accepted); }
    void SetConsent(bool accepted)
    {
        privacyAccepted = accepted;
        root.Q<Toggle>("register-consent")?.SetValueWithoutNotify(accepted);
        root.Q("register-submit")?.SetEnabled(accepted);
        if (accepted) SetStatus("register-status", "");
    }
    // An eye button inside each password box shows what was typed, and hides it again (back to asterisks).
    // Passwords start hidden, and are hidden again whenever the page or the account modal changes.
    readonly List<(TextField field, Button eye, MenuLineArt icon)> passwordToggles = new List<(TextField, Button, MenuLineArt)>();
    void BindPasswordToggle(string name)
    {
        var field = root.Q<TextField>(name);
        var eye = root.Q<Button>(name + "-eye");
        var icon = eye?.Q<MenuLineArt>();
        if (field == null || eye == null || icon == null) return;
        passwordToggles.Add((field, eye, icon));
        On(eye, () => ShowPassword(field, eye, icon, field.isPasswordField));
        ShowPassword(field, eye, icon, false);
    }
    static void ShowPassword(TextField field, Button eye, MenuLineArt icon, bool show)
    {
        field.isPasswordField = !show;
        icon.Kind = show ? "eye-off" : "eye";
        eye.tooltip = show ? "Hide password" : "Show password";
    }
    void HidePasswords() { foreach (var (field, eye, icon) in passwordToggles) ShowPassword(field, eye, icon, false); }
    void PlayOffline()
    {
        if (signingIn || !Application.isPlaying) return;
        root.Q<Label>("login-status").text = "";
        MindscapeCloud.PlayOffline();
        ShowPage("home"); Notify("Playing offline. Your progress is saved on this device. Pick up where you left off from Play Game.");
    }
    void SetStatus(string label, string text) { var status = root.Q<Label>(label); if (status != null) status.text = text; }
    bool LoginErrorOpen => root.Q("login-error-modal") is VisualElement modal && !modal.ClassListContains("hidden");
    void OpenLoginError(string message)
    {
        SetStatus("login-status", "");
        HidePasswords();
        root.Q<Label>("login-error-body").text = string.IsNullOrWhiteSpace(message)
            ? "Could not sign in. Please try again." : message;
        root.Q("login-error-modal").RemoveFromClassList("hidden");
        root.schedule.Execute(() => root.Q<Button>("login-error-close")?.Focus()).StartingIn(50);
    }
    void CloseLoginError()
    {
        root.Q("login-error-modal")?.AddToClassList("hidden");
        root.Q<TextField>("login-password")?.Focus();
    }
    void SetLoginLoading(bool busy)
    {
        if (root == null) return;
        root.Q("login-spinner")?.EnableInClassList("hidden", !busy);
        var submit = root.Q<Button>("login-submit");
        if (submit != null) { submit.text = busy ? "Signing in…" : "Log in"; submit.EnableInClassList("loading", busy); submit.SetEnabled(!busy); }
        foreach (var name in new[] { "login-username", "login-password", "login-offline", "login-register", "login-forgot", "login-recover", "login-google" })
            root.Q(name)?.SetEnabled(!busy);
    }
    string Field(string name) => root.Q<TextField>(name)?.value ?? "";
    void ClearFields(params string[] names) { foreach (var name in names) { var field = root.Q<TextField>(name); if (field != null) field.value = ""; } }

    async void SignIn()
    {
        if (signingIn || !Application.isPlaying) return;
        string identifier = Field("login-username").Trim(), password = Field("login-password");
        if (identifier.Length == 0 || password.Length == 0) { OpenLoginError("Enter your username or email, and your password."); return; }
        signingIn = true; SetStatus("login-status", ""); SetLoginLoading(true);
        string error;
        try { error = await MindscapeCloud.Login(identifier, password); }
        catch (Exception ex) { Debug.LogException(ex); error = "Could not reach the server. Check your connection and try again."; }
        finally { signingIn = false; if (this != null && root != null) SetLoginLoading(false); }
        if (this == null || root == null) return;
        // Signed in but the journal failed to download: still let them in; the journal page waits for it.
        if (!MindscapeCloud.IsSignedIn)
        {
            // The account exists but its email was never confirmed: send a fresh code and open the code box.
            if (MindscapeCloud.UnverifiedEmail != null) { pendingEmail = MindscapeCloud.UnverifiedEmail; await MindscapeCloud.ResendSignup(pendingEmail); OpenModal(AccountStep.SignupCode, "Your email is not confirmed yet. We sent a new code to " + pendingEmail + "."); }
            else OpenLoginError(error);
            return;
        }
        ClearFields("login-password"); SetStatus("login-status", "");
        ShowPage("home"); Notify("Welcome, " + MindscapeCloud.Username + ".");
    }

    // Sign-up checks while the player types. Leaving the username or email box asks the server whether it is free
    // (and whether the email is Gmail or Yahoo); the message shows under that box. The password boxes drive a
    // strength bar. Typing again clears a box's old message.
    readonly Dictionary<string, int> fieldChecks = new Dictionary<string, int>();
    void BindSignupChecks()
    {
        foreach (var name in new[] { "register-username", "register-email" })
        {
            string field = name;
            var box = root.Q<TextField>(field);
            if (box == null) continue;
            box.RegisterCallback<FocusOutEvent>(_ => CheckField(field));
            box.RegisterValueChangedCallback(_ => { fieldChecks[field] = fieldChecks.TryGetValue(field, out int n) ? n + 1 : 1; SetFieldStatus(field, ""); });
        }
        root.Q<TextField>("register-password")?.RegisterValueChangedCallback(evt => UpdateStrength("register", evt.newValue));
        root.Q<TextField>("modal-password")?.RegisterValueChangedCallback(evt => UpdateStrength("modal", evt.newValue));
        SetFieldStatus("register-username", ""); SetFieldStatus("register-email", "");
        UpdateStrength("register", Field("register-password")); UpdateStrength("modal", Field("modal-password"));
    }

    void SetFieldStatus(string field, string message, bool ok = false)
    {
        var label = root.Q<Label>(field + "-status");
        if (label == null) return;
        label.text = message ?? "";
        label.EnableInClassList("ok", ok);
        label.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
    }

    static readonly System.Text.RegularExpressions.Regex UsernamePattern = new System.Text.RegularExpressions.Regex(@"^[A-Za-z0-9_.]{3,24}$");
    const string UsernameRule = "Usernames are 3-24 characters: letters, numbers, _ or .";

    async void CheckField(string field)
    {
        if (!Application.isPlaying) return;
        string value = Field(field).Trim();
        if (value.Length == 0) { SetFieldStatus(field, ""); return; }
        bool email = field == "register-email";
        // Format problems are shown straight away; only a well-formed value goes to the server.
        if (!email && !UsernamePattern.IsMatch(value)) { SetFieldStatus(field, UsernameRule); return; }
        if (email && !value.Contains("@")) { SetFieldStatus(field, "Enter a valid email address."); return; }
        if (email && !MindscapeCloud.IsAllowedEmail(value)) { SetFieldStatus(field, MindscapeCloud.EmailDomainRule); return; }
        int check = fieldChecks.TryGetValue(field, out int n) ? n : 0;
        var (usernameError, emailError) = await MindscapeCloud.CheckAvailable(email ? "" : value, email ? value : "");
        if (this == null || root == null || (fieldChecks.TryGetValue(field, out int now) ? now : 0) != check) return;   // typed again meanwhile
        string error = email ? emailError : usernameError;
        SetFieldStatus(field, error ?? (email ? "This email can be used." : "This username is free."), error == null);
    }

    // The bar fills one fifth per rule met: 8+ characters, upper case, lower case, number, symbol.
    void UpdateStrength(string prefix, string password)
    {
        var fill = root.Q(prefix + "-strength-fill");
        var label = root.Q<Label>(prefix + "-strength-label");
        if (fill == null || label == null) return;
        password ??= "";
        var missing = new List<string>();
        if (password.Length < 8) missing.Add("8+ characters");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[A-Z]")) missing.Add("an upper case letter");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[a-z]")) missing.Add("a lower case letter");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[0-9]")) missing.Add("a number");
        if (!System.Text.RegularExpressions.Regex.IsMatch(password, "[^A-Za-z0-9]")) missing.Add("a symbol");
        int met = 5 - missing.Count;
        string level = met <= 2 ? "Weak" : met == 3 ? "Fair" : met == 4 ? "Good" : "Strong";
        fill.style.width = Length.Percent(password.Length == 0 ? 0 : met * 20f);
        fill.EnableInClassList("fair", level == "Fair");
        fill.EnableInClassList("good", level == "Good");
        fill.EnableInClassList("strong", level == "Strong");
        if (password.Length == 0) label.text = "Use 8+ characters with upper and lower case letters, a number and a symbol.";
        else if (missing.Count == 0) label.text = "Strong password.";
        else
        {
            string list = missing.Count == 1 ? missing[0] : string.Join(", ", missing.GetRange(0, missing.Count - 1)) + " and " + missing[missing.Count - 1];
            label.text = level + ": add " + list + ".";
        }
    }

    // Continue with Google: the browser opens for the Google sign-in while the modal waits for it. A first-time
    // Google player then chooses a username, and the account is created.
    async void StartGoogle()
    {
        if (signingIn || !Application.isPlaying) return;
        SetStatus("login-status", "");
        MindscapeCloud.StartGoogle();
        OpenModal(AccountStep.GoogleWait);
        string error = await MindscapeCloud.WaitForGoogle();
        if (this == null || root == null || error == "" || accountStep != AccountStep.GoogleWait) return;   // cancelled
        if (MindscapeCloud.IsSignedIn)
        {
            CloseModal(); ClearFields("login-password"); SetStatus("login-status", "");
            ShowPage("home"); Notify("Welcome, " + MindscapeCloud.Username + ".");
            return;
        }
        if (MindscapeCloud.GoogleNeedsUsername) { OpenModal(AccountStep.GoogleUsername); return; }
        CloseModal();
        SetStatus("login-status", error ?? "Google sign-in did not finish. Try again.");
    }

    // Create account: send the code, then ask for it in the modal. The account exists once the code matches.
    async void StartSignup()
    {
        if (signingIn || !Application.isPlaying) return;
        if (!privacyAccepted) { SetStatus("register-status", "Read the Data Privacy Notice and agree to it before we send your code."); return; }
        string username = Field("register-username").Trim(), email = Field("register-email").Trim(), password = Field("register-password");
        if (username.Length == 0 || email.Length == 0 || password.Length == 0) { SetStatus("register-status", "Enter a username, an email and a password."); return; }
        if (!UsernamePattern.IsMatch(username)) { SetFieldStatus("register-username", UsernameRule); SetStatus("register-status", UsernameRule); return; }
        if (!MindscapeCloud.IsAllowedEmail(email)) { SetFieldStatus("register-email", MindscapeCloud.EmailDomainRule); SetStatus("register-status", MindscapeCloud.EmailDomainRule); return; }
        if (!MindscapeCloud.IsStrongPassword(password)) { SetStatus("register-status", "Your password is not strong enough. " + MindscapeCloud.PasswordRule); return; }
        signingIn = true; SetStatus("register-status", "Sending your code…");
        string error = await MindscapeCloud.SignUp(username, email, password);
        signingIn = false;
        if (this == null || root == null) return;
        if (error != null)
        {
            // Username or email already used: say so under that box too.
            if (MindscapeCloud.SignupField != null) SetFieldStatus("register-" + MindscapeCloud.SignupField, error);
            SetStatus("register-status", error);
            return;
        }
        SetStatus("register-status", "");
        pendingEmail = email;
        OpenModal(AccountStep.SignupCode);
    }

    // The account modal: confirm a new account's email, reset a password (who → code → new password), delete the
    // account (are you sure?), or recover a deleted one (email → code → are you sure? → new password).
    enum AccountStep { None, SignupCode, ResetStart, ResetCode, ResetPassword, DeleteConfirm, RecoverStart, RecoverCode, RecoverConfirm, RecoverPassword, GoogleWait, GoogleUsername }
    AccountStep accountStep;
    string pendingEmail, pendingIdentifier;

    static string Day(DateTime when) => when == DateTime.MinValue ? "" : when.ToString("d MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture);

    void OpenModal(AccountStep step, string message = null)
    {
        accountStep = step;
        bool recoverStart = step == AccountStep.RecoverStart;
        bool googleWait = step == AccountStep.GoogleWait, googleName = step == AccountStep.GoogleUsername;
        bool who = step == AccountStep.ResetStart || recoverStart || googleName;
        bool code = step == AccountStep.SignupCode || step == AccountStep.ResetCode || step == AccountStep.RecoverCode;
        bool password = step == AccountStep.ResetPassword || step == AccountStep.RecoverPassword;
        root.Q("modal-identifier")?.EnableInClassList("hidden", !who);
        root.Q("modal-code")?.EnableInClassList("hidden", !code);
        root.Q("modal-password-row")?.EnableInClassList("hidden", !password);
        root.Q("modal-password2-row")?.EnableInClassList("hidden", !password);
        root.Q("modal-strength")?.EnableInClassList("hidden", !password);
        root.Q("modal-resend")?.EnableInClassList("hidden", !code && !googleWait);
        root.Q("modal-submit")?.EnableInClassList("hidden", googleWait);
        var resend = root.Q<Button>("modal-resend");
        if (resend != null) resend.text = googleWait ? (GoogleSignInSheet.InApp ? "Open sign-in again" : "Open browser again") : "Send again";
        string name = MindscapeCloud.RecoveryUsername;
        string title, body, submit;
        switch (step)
        {
            case AccountStep.SignupCode: title = "Check your email"; body = "We sent a code to " + pendingEmail + ". Enter it to create your account."; submit = "Verify"; break;
            case AccountStep.ResetStart: title = "Reset password"; body = "Enter your username or email and we will email you a code."; submit = "Send code"; break;
            case AccountStep.ResetCode: title = "Enter your code"; body = "If an account exists, a code is on its way. Enter it below. It can take a minute to arrive."; submit = "Verify"; break;
            case AccountStep.DeleteConfirm:
                title = "Delete your account?";
                body = "This signs " + MindscapeCloud.Username + " out on every device and emails you. Your account, journal and progress are kept for 30 days, and you can recover them from the login screen until then. After 30 days they are permanently deleted.";
                submit = "Delete account"; break;
            case AccountStep.RecoverStart: title = "Recover a deleted account"; body = "Enter the email of the account you deleted. If it can still be recovered, we will email you a code."; submit = "Send code"; break;
            case AccountStep.RecoverCode: title = "Enter your code"; body = "If a deleted account uses " + pendingEmail + ", a code is on its way. Enter it below. It can take a minute to arrive."; submit = "Verify"; break;
            case AccountStep.RecoverConfirm:
                title = "Recover this account?";
                body = "Are you sure you want to recover " + name + "? It was deleted on " + Day(MindscapeCloud.RecoveryDeletedOn) + " and will be permanently deleted on " + Day(MindscapeCloud.RecoveryDeleteAfter) + " unless you recover it. Next, you will choose a new password.";
                submit = "Yes, recover it"; break;
            case AccountStep.RecoverPassword: title = "New password"; body = "Choose a new password for " + name + ". Your account comes back once it is saved."; submit = "Recover account"; break;
            case AccountStep.GoogleWait:
                title = "Continue with Google";
                body = GoogleSignInSheet.InApp
                    ? "Sign in with Google in the window that opened. You come back here by yourself when you finish. If it closed, choose Open sign-in again."
                    : "Finish signing in with Google in your browser, then come back here. This updates by itself. If no browser opened, choose Open browser again.";
                submit = ""; break;
            case AccountStep.GoogleUsername:
                title = "Choose a username";
                body = "You are signed in with Google. Choose a username for your Mindscape account: 3-24 letters, numbers, _ or .";
                submit = "Create account"; break;
            default: title = "New password"; body = "The code matched. Choose a new password."; submit = "Save password"; break;
        }
        root.Q<Label>("modal-title").text = title;
        root.Q<Label>("modal-body").text = message ?? body;
        var submitButton = root.Q<Button>("modal-submit");
        submitButton.text = submit;
        submitButton.EnableInClassList("danger-button", step == AccountStep.DeleteConfirm);
        SetStatus("modal-status", "");
        ClearFields("modal-code", "modal-password", "modal-password2"); HidePasswords();
        if (googleName)
        {
            var id = root.Q<TextField>("modal-identifier");
            id.label = "Username";
            id.value = MindscapeCloud.GoogleSuggestion ?? "";
        }
        else if (who)
        {
            var id = root.Q<TextField>("modal-identifier");
            id.label = recoverStart ? "Email" : "Username or email";
            string typed = Field("login-username").Trim();
            if (recoverStart && !id.value.Contains("@")) id.value = typed.Contains("@") ? typed : "";
            else if (id.value.Length == 0) id.value = typed;
        }
        root.Q("account-modal").RemoveFromClassList("hidden");
        string focus = who ? "modal-identifier" : code ? "modal-code" : password ? "modal-password" : null;
        if (focus != null) root.schedule.Execute(() => root.Q<TextField>(focus)?.Focus()).StartingIn(50);
    }

    void CloseModal()
    {
        if (accountStep == AccountStep.ResetCode || accountStep == AccountStep.ResetPassword || accountStep == AccountStep.RecoverConfirm || accountStep == AccountStep.RecoverPassword)
            MindscapeCloud.CancelReset();
        if (accountStep == AccountStep.GoogleWait || accountStep == AccountStep.GoogleUsername) MindscapeCloud.CancelGoogle();
        accountStep = AccountStep.None;
        root.Q("account-modal")?.AddToClassList("hidden");
        ClearFields("modal-code", "modal-password", "modal-password2");
    }

    async void SubmitModal()
    {
        if (signingIn || !Application.isPlaying || accountStep == AccountStep.None) return;
        var step = accountStep;
        string error = null;
        if (step == AccountStep.GoogleWait) return;
        if (step == AccountStep.GoogleUsername)
        {
            string username = Field("modal-identifier").Trim();
            if (!UsernamePattern.IsMatch(username)) { SetStatus("modal-status", UsernameRule); return; }
            signingIn = true; SetStatus("modal-status", "Creating your account…");
            error = await MindscapeCloud.ChooseGoogleUsername(username);
            signingIn = false;
            if (this == null || root == null) return;
            if (!MindscapeCloud.IsSignedIn) { SetStatus("modal-status", error); return; }
            CloseModal(); ClearFields("login-password"); SetStatus("login-status", "");
            ShowPage("home"); Notify("Account created. Welcome, " + MindscapeCloud.Username + ".");
            return;
        }
        if (step == AccountStep.ResetStart || step == AccountStep.RecoverStart)
        {
            string identifier = Field("modal-identifier").Trim();
            bool recover = step == AccountStep.RecoverStart;
            if (identifier.Length == 0 || (recover && !identifier.Contains("@"))) { SetStatus("modal-status", recover ? "Enter the email of the account you deleted." : "Enter your username or email."); return; }
            signingIn = true; SetStatus("modal-status", "Sending your code…");
            error = recover ? await MindscapeCloud.RequestRecovery(identifier) : await MindscapeCloud.RequestReset(identifier);
            signingIn = false;
            if (this == null || root == null) return;
            if (error != null) { SetStatus("modal-status", error); return; }
            if (recover) { pendingEmail = identifier; OpenModal(AccountStep.RecoverCode); }
            else { pendingIdentifier = identifier; OpenModal(AccountStep.ResetCode); }
            return;
        }
        if (step == AccountStep.DeleteConfirm)
        {
            signingIn = true; SetStatus("modal-status", "Deleting your account…");
            error = await MindscapeCloud.DeleteAccount();
            signingIn = false;
            if (this == null || root == null) return;
            if (error != null) { SetStatus("modal-status", error); return; }
            // Signing out already brought the login page back (OnCloudChanged).
            CloseModal(); ShowPage("login");
            SetStatus("login-status", "Your account was deleted and you are signed out. We emailed you: until " + Day(MindscapeCloud.DeletedUntil) + " you can recover it with \"Recover a deleted account\".");
            return;
        }
        if (step == AccountStep.RecoverConfirm) { OpenModal(AccountStep.RecoverPassword); return; }
        if (step == AccountStep.ResetPassword || step == AccountStep.RecoverPassword)
        {
            string password = Field("modal-password");
            if (!MindscapeCloud.IsStrongPassword(password)) { SetStatus("modal-status", MindscapeCloud.PasswordRule); return; }
            if (password != Field("modal-password2")) { SetStatus("modal-status", "The two passwords do not match."); return; }
            bool recover = step == AccountStep.RecoverPassword;
            signingIn = true; SetStatus("modal-status", recover ? "Recovering your account…" : "Saving…");
            error = recover ? await MindscapeCloud.RecoverAccount(password) : await MindscapeCloud.SetNewPassword(password);
            signingIn = false;
            if (this == null || root == null) return;
            if (!MindscapeCloud.IsSignedIn) { SetStatus("modal-status", error); return; }
            CloseModal(); ClearFields("login-password"); SetStatus("login-status", "");
            ShowPage("home"); Notify(recover ? "Your account is back. Welcome back, " + MindscapeCloud.Username + "." : "Password updated. Welcome back, " + MindscapeCloud.Username + ".");
            return;
        }
        string code = Field("modal-code").Trim();
        if (code.Length == 0) { SetStatus("modal-status", "Enter the code from your email."); return; }
        signingIn = true; SetStatus("modal-status", "Checking…");
        error = step == AccountStep.SignupCode ? await MindscapeCloud.VerifySignup(pendingEmail, code)
            : step == AccountStep.RecoverCode ? await MindscapeCloud.VerifyRecovery(pendingEmail, code)
            : await MindscapeCloud.VerifyReset(pendingIdentifier, code);
        signingIn = false;
        if (this == null || root == null) return;
        if (step == AccountStep.ResetCode || step == AccountStep.RecoverCode)
        {
            if (error != null) SetStatus("modal-status", error); else OpenModal(step == AccountStep.ResetCode ? AccountStep.ResetPassword : AccountStep.RecoverConfirm);
            return;
        }
        if (!MindscapeCloud.IsSignedIn) { SetStatus("modal-status", error); return; }
        CloseModal(); ClearFields("register-username", "register-email", "register-password", "login-password"); SetConsent(false);
        SetFieldStatus("register-username", ""); SetFieldStatus("register-email", "");
        ShowPage("home"); Notify("Account created. Welcome, " + MindscapeCloud.Username + ".");
    }

    async void ResendCode()
    {
        if (signingIn || !Application.isPlaying) return;
        if (accountStep == AccountStep.GoogleWait) { MindscapeCloud.OpenGoogleAgain(); return; }
        signingIn = true; SetStatus("modal-status", "Sending a new code…");
        string error = accountStep == AccountStep.SignupCode ? await MindscapeCloud.ResendSignup(pendingEmail)
            : accountStep == AccountStep.RecoverCode ? await MindscapeCloud.RequestRecovery(pendingEmail)
            : await MindscapeCloud.RequestReset(pendingIdentifier);
        signingIn = false;
        if (this == null || root == null) return;
        SetStatus("modal-status", error ?? "A new code is on its way.");
    }
    // Options > Account: who is signed in, with Sign out and Delete account; offline players get Log in instead.
    void RefreshAccountPanel()
    {
        if (root == null) return;
        bool online = MindscapeCloud.IsSignedIn;
        SetStatus("options-account-name", online ? "Signed in as " + MindscapeCloud.Username + "." : "Playing offline. Your journal and progress are saved on this device only.");
        root.Q("options-account-online")?.EnableInClassList("hidden", !online);
        root.Q("options-account-offline")?.EnableInClassList("hidden", online);
    }
    async void RefreshCloud()
    {
        string error = await MindscapeCloud.Refresh();
        if (error != null && this != null && root != null && MindscapeCloud.IsSignedIn) Notify("Could not load your journal. " + error);
    }
    void OnCloudChanged()
    {
        if (root == null) return;
        var name = root.Q<Label>("account-name"); if (name != null) name.text = AccountLabel;
        var action = root.Q<Button>("sign-out"); if (action != null) action.text = AccountAction;
        RefreshAccountPanel();
        if (NeedsLogin) ShowPage("login");
        else if (currentPage == "journal") LoadJournal(journalIndex);
        else if (currentPage == "continue" && !ContinueAvailable) ShowPage("home");
        RefreshLocks(); RefreshHome();
    }
    void BindHome()
    {
        var name = root.Q<Label>("account-name"); if (name != null) name.text = AccountLabel;
        var action = root.Q<Button>("sign-out"); if (action != null) action.text = AccountAction;
        On(action, () => MindscapeCloud.SignOut());
        foreach (var target in new[] { "chapters", "continue", "journal", "options", "guide", "resources", "about", "quit" })
        {
            string page = target;
            On("open-" + page, () => ShowPage(page));
        }
    }
    void BindChapters()
    {
        for (int i = 1; i <= 3; i++)
        {
            int chapter = i;
            var card = root.Q<Button>("chapter-" + chapter);
            if (card == null) continue;
            On(card, () =>
            {
                if (MindscapeProgress.IsChapterUnlocked(chapter)) ShowPage("episodes-" + chapter);
                else Notify("Finish Chapter " + (chapter - 1) + " to unlock Chapter " + chapter + ".");
            });
            chapterCards.Add((card, root.Q<Label>("chapter-" + chapter + "-state"), chapter));
        }
        progressCaption = root.Q<Label>("progress-caption");
        progressFill = root.Q("progress-fill");
    }

    // Opens an episode only when progression allows it.
    void PlayEpisode(int chapter, int episode, string scene, int saveEpisode)
    {
        if (!MindscapeProgress.IsEpisodeUnlocked(chapter, episode))
        {
            Notify(episode == 1 ? "Finish Chapter " + (chapter - 1) + " to unlock this chapter." : "Finish Episode " + (episode - 1) + " to unlock Episode " + episode + ".");
            return;
        }
        StartScene(scene, chapter, saveEpisode, true);
    }

    void RefreshLocks()
    {
        // Once a player has made progress, the episode to pick up next is marked (this is how offline play resumes).
        int nextChapter = 0, nextSlot = 0;
        bool resume = MindscapeProgress.CompletedTotal > 0 && MindscapeProgress.NextUnfinished(out nextChapter, out nextSlot);
        foreach (var (card, bottom, chapter) in chapterCards)
        {
            bool open = MindscapeProgress.IsChapterUnlocked(chapter);
            card.EnableInClassList("locked", !open);
            if (bottom != null) bottom.text = open ? MindscapeProgress.CompletedIn(chapter) + " / " + MindscapeProgress.EpisodeCount(chapter) + " episodes complete" +
                (resume && chapter == nextChapter ? "  ·  continue with Episode " + (nextSlot - 1) : "") : "LOCKED  ·  finish Chapter " + (chapter - 1);
        }
        foreach (var (row, state, chapter, episode) in episodeRows)
        {
            bool open = MindscapeProgress.IsEpisodeUnlocked(chapter, episode), next = resume && chapter == nextChapter && episode == nextSlot - 1;
            row.EnableInClassList("available", open); row.EnableInClassList("locked", !open); row.EnableInClassList("next", next);
            if (state != null) state.text = !open ? "LOCKED" : MindscapeProgress.IsComplete(chapter, episode) ? "REPLAY  ›" : next ? "CONTINUE  ›" : "PLAY  ›";
        }
        if (progressCaption == null || progressFill == null) return;
        int done = MindscapeProgress.CompletedTotal, total = MindscapeProgress.TotalEpisodes;
        progressCaption.text = done + " of " + total + " story episodes complete";
        progressFill.style.width = new Length(100f * done / total, LengthUnit.Percent);
    }

    // The episode rows are in the UXML (one Button per episode, named below, with a "<name>-state" label).
    void BindEpisodes()
    {
        void Row(string name, int chapter, int episode, Action play)
        {
            var row = root.Q<Button>(name);
            if (row == null) { Debug.LogWarning("Main menu: no '" + name + "' row in MainMenuNew.uxml."); return; }
            On(row, play);
            episodeRows.Add((row, root.Q<Label>(name + "-state"), chapter, episode));
        }
        // Chapter 1 · Academic Pressure: four episodes around one school deadline.
        for (int i = 0; i < AcademicPressureChapter.EpisodeCount; i++) { int index = i; Row("academic-episode-" + (i + 1), 1, index + 1, () => PlayEpisode(1, index + 1, AcademicPressureChapter.Scenes[index], index + 2)); }
        for (int i = 0; i < episodeNames.Length; i++) { int index = i; Row("episode-" + (i + 1), 2, index + 1, () => PlayEpisode(2, index + 1, FamilyScene(index + 2), index + 2)); }
        for (int i = 0; i < Mindscape.SocialDynamics.SocialDynamicsChapter.EpisodeCount; i++)
        { int episode = i + 1; Row("social-episode-" + episode, 3, episode, () => PlayEpisode(3, episode, Mindscape.SocialDynamics.SocialDynamicsChapter.Scene(episode), episode + 1)); }
        On("play-tutorial-1", () => StartScene(tutorialScene, 1, 1, true));
        On("play-tutorial", () => StartScene(tutorialScene, 1, 1, true));
    }
    string FamilyScene(int episode) => episode >= 5 ? Mindscape.FamilyFinale.FamilyFinaleEpisode.Scene(episode - 1) : episode == 4 ? Mindscape.EpisodeThree.EpisodeThreeStory.SceneName : episode == 3 ? Mindscape.EpisodeTwo.EpisodeTwoStory.SceneName : episodeScene;

    // Continue lists this player's three save slots (MindscapeSaves, made from the pause menu), online or offline,
    // and is shown once at least one slot is used. Loading a slot continues where it was saved (older saves restart
    // their episode); a slot can be deleted to free it.
    static bool ContinueAvailable => MindscapeSaves.HasAny;
    void RefreshHome() => root.Q("open-continue")?.EnableInClassList("hidden", !ContinueAvailable);
    // The slots depend on what was saved, so they are made here into the UXML's "save-content".
    void RefreshContinue()
    {
        var content = root.Q("save-content"); content.Clear(); content.Add(new PaperGrain());
        Text(content, "YOUR SAVED GAMES", "eyebrow");
        for (int i = 0; i < MindscapeSaves.SlotCount; i++)
        {
            var saved = MindscapeSaves.Get(i);
            if (saved == null) { Text(Box(content, "save-slot empty-slot"), "Slot " + (i + 1) + "     —     Empty. Save from the pause menu during an episode.", "small"); continue; }
            int chapter = saved.chapter, episode = saved.episode;
            var slot = Box(content, "save-slot");
            Box(slot, "thumbnail").style.backgroundImage = Resources.Load<Texture2D>(EpisodeLoadingScreen.Thumbnail(chapter, episode));
            var copy = Box(slot, "grow");
            Text(copy, "Slot " + (i + 1) + "  ·  " + EpisodeReflection.Label(chapter, episode), "episode-name");
            Text(copy, EpisodeReflection.Title(chapter, episode), "small margin-top");
            bool resumes = MindscapeSaves.Resumes(saved);
            Text(copy, saved.playerName + "  ·  saved " + MindscapeSaves.When(saved) + (resumes ? "  ·  continues where you saved" : "  ·  restarts this episode"), "small margin-top");
            var actions = Box(copy, "save-actions margin-top");
            int index = i;
            Button(actions, "Resume  ›", () =>
            {
                string scene = StoryScene(chapter, episode);
                if (scene == null) { Notify("This saved episode is not available in this build."); return; }
                MindscapeSaves.SetPending(saved);
                StartScene(scene, chapter, episode + 1, false);   // the menu's save convention: story episodes start at 2
            }, "save-resume", "resume-save-" + (i + 1));
            // Deleting asks once more: the first press turns the button into "Delete for good?".
            Button delete = null;
            bool armed = false;
            delete = Button(actions, "Delete", () =>
            {
                if (!armed)
                {
                    armed = true; delete.text = "Delete for good?"; delete.AddToClassList("armed");
                    delete.schedule.Execute(() => { armed = false; delete.text = "Delete"; delete.RemoveFromClassList("armed"); }).StartingIn(4000);
                    return;
                }
                MindscapeSaves.Delete(index);
                Notify("Slot " + (index + 1) + " deleted.");
                RefreshContinue(); RefreshHome();
            }, "save-delete", "delete-save-" + (i + 1));
        }
    }
    // The scene that starts a story episode (1-based), or null.
    string StoryScene(int chapter, int episode) =>
        chapter == 1 && episode >= 1 && episode <= AcademicPressureChapter.EpisodeCount ? AcademicPressureChapter.Scenes[episode - 1]
        : chapter == 2 && episode >= 1 && episode <= 5 ? FamilyScene(episode + 1)
        : chapter == 3 && episode >= 1 && episode <= Mindscape.SocialDynamics.SocialDynamicsChapter.EpisodeCount ? Mindscape.SocialDynamics.SocialDynamicsChapter.Scene(episode) : null;

    // ---------------------------------------------------------------- journal
    // The Self-reflection tab is in the UXML; one tab per story episode follows it, under a heading per chapter, in
    // chapter and episode order ("journal-episode-tabs"). An episode's tab opens once the episode is unlocked or has
    // an entry. journalIndex is chapter * 100 + episode, or SelfReflection.
    readonly List<(Button tab, int key)> journalTabs = new List<(Button, int)>();
    void BindJournal()
    {
        journal = root.Q<TextField>("reflection-text");
        journalSummary = root.Q("journal-summary"); journalSummaryText = root.Q<Label>("journal-summary-text"); journalActions = root.Q("journal-actions");
        On("journal-tab-self", () => LoadJournal(SelfReflection));
        journalTabs.Clear();
        var box = root.Q("journal-episode-tabs");
        if (box != null)
        {
            box.Clear();
            for (int chapter = 1; chapter <= MindscapeProgress.ChapterCount; chapter++)
            {
                Text(box, "CHAPTER " + chapter + "  ·  " + EpisodeReflection.ChapterName(chapter).ToUpperInvariant(), "journal-chapter");
                for (int episode = 1; episode <= MindscapeProgress.EpisodeCount(chapter); episode++)
                {
                    int key = chapter * 100 + episode;
                    journalTabs.Add((Button(box, "EP. " + episode + "\n" + EpisodeReflection.Title(chapter, episode), () => LoadJournal(key), "tab journal-episode", "journal-tab-" + chapter + "-" + episode), key));
                }
            }
        }
        On("journal-clear", () => { journal.value = ""; root.Q<Label>("journal-status").text = "Entry cleared. Save reflection to keep this change."; });
        On("save-reflection", () => { SaveJournalEntry(journalIndex, journal.value); root.Q<Label>("journal-status").text = MindscapeCloud.IsSignedIn ? "Saved to your account." : "Saved on this device."; });
        journal.RegisterValueChangedCallback(evt => root.Q<Label>("journal-status").text = "Unsaved changes");
    }
    void LoadJournal(int index)
    {
        if (journal == null) return;
        // Preserve edits when navigating between entries or leaving the journal.
        if (currentPage == "journal" && journalIndex != SelfReflection && root.Q<Label>("journal-status").text == "Unsaved changes")
            SaveJournalEntry(journalIndex, journal.value);
        journalIndex = index;
        bool summary = index == SelfReflection;
        journal.EnableInClassList("hidden", summary); journalActions.EnableInClassList("hidden", summary); journalSummary.EnableInClassList("hidden", !summary);
        root.Q("journal-tab-self")?.EnableInClassList("selected", summary);
        foreach (var (tab, key) in journalTabs)
        {
            tab.EnableInClassList("selected", key == index);
            tab.SetEnabled(key == index || MindscapeProgress.IsEpisodeUnlocked(key / 100, key % 100) || JournalEntry(key / 100, key % 100).Length > 0);
        }
        var prompt = root.Q<Label>("journal-prompt");
        bool ready = !MindscapeCloud.IsSignedIn || MindscapeCloud.IsLoaded;
        if (summary)
        {
            root.Q<Label>("journal-title").text = "Self-reflection";
            if (prompt != null) prompt.text = "Your self-assessment answers and stress check-ins, by chapter and episode.";
            journalSummaryText.text = SelfReflectionText();
            root.Q<Label>("journal-status").text = !ready ? "Loading your answers…" : MindscapeCloud.IsSignedIn ? "Saved to your account." : "Saved on this device.";
            return;
        }
        int chapter = index / 100, episode = index % 100;
        journal.SetValueWithoutNotify(JournalEntry(chapter, episode));
        root.Q<Label>("journal-title").text = EpisodeReflection.Label(chapter, episode) + "  ·  " + EpisodeReflection.Title(chapter, episode);
        if (prompt != null) prompt.text = EpisodeReflection.Prompt(chapter, episode);
        // Until the account's entries arrive, editing could overwrite them with an empty page.
        journal.SetEnabled(ready); root.Q("save-reflection").SetEnabled(ready);
        root.Q<Label>("journal-status").text = !MindscapeCloud.IsSignedIn ? "Your reflections stay on this device." : ready ? "Your reflections are saved to your account." : "Loading your journal…";
    }
    const int SelfReflection = -3;

    // Answers and check-ins in chapter and episode order (several runs of one episode: oldest first).
    string SelfReflectionText()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append("<b>SELF-REFLECTION ASSESSMENT</b>\n");
        var assessments = MindscapeDatabase.GetAssessments();
        assessments.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.episode != b.episode ? a.episode.CompareTo(b.episode) : string.CompareOrdinal(a.created_at, b.created_at));
        if (assessments.Count == 0) sb.Append("Not answered yet. You can answer it at the end of each episode.\n");
        foreach (var a in assessments)
            sb.Append((a.episode > 0 ? EpisodeReflection.Label(a.chapter, a.episode) + "  " + EpisodeReflection.Title(a.chapter, a.episode) : "Chapter " + a.chapter) + "  ·  " + When(a.created_at) +
                      "\n    How overwhelmed: " + EpisodeReflection.OverwhelmLevels[Mathf.Clamp(a.overwhelm_level - 1, 0, 4)] + "\n    Most useful coping strategy: " + a.coping_strategy + "\n");
        sb.Append("\n<b>STRESS CHECK-INS AND TIME, BY EPISODE</b>\n");
        var records = MindscapeDatabase.GetEpisodeRecords();
        records.Sort((a, b) => a.chapter != b.chapter ? a.chapter.CompareTo(b.chapter) : a.episode != b.episode ? a.episode.CompareTo(b.episode) : string.CompareOrdinal(a.created_at, b.created_at));
        if (records.Count == 0) sb.Append("Nothing yet. At the end of each episode, you are asked how stressed you feel.\n");
        foreach (var r in records)
        {
            // Older records also have a check-in from before the episode; newer ones are asked only afterwards.
            string checkIn = (r.checkin_start > 0 ? r.checkin_start + " / 5  →  " : "") + (r.checkin_end > 0 ? r.checkin_end + " / 5" : "—");
            sb.Append(EpisodeReflection.Label(r.chapter, r.episode) + "  " + EpisodeReflection.Title(r.chapter, r.episode) + "  ·  " + When(r.created_at) + "\n    " +
                      (r.result == "time_up" ? "Ran out of time" : "Completed") + "  ·  check-in " + checkIn + "  ·  stress peak " + r.stress_peak + ", end " + r.stress_final +
                      "  ·  " + Clock(r.seconds_left) + " left of " + Clock(r.time_limit) + "\n");
        }
        return sb.ToString();
    }

    static string Clock(int seconds) => (Mathf.Max(0, seconds) / 60).ToString("00") + ":" + (Mathf.Max(0, seconds) % 60).ToString("00");

    static string When(string stamp) =>
        DateTime.TryParse(stamp, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToLocalTime().ToString("d MMM yyyy, h:mm tt", System.Globalization.CultureInfo.InvariantCulture) : stamp;
    void SaveJournalEntry(int index, string text)
    {
        if (index == SelfReflection) return;
        int chapter = index / 100, episode = index % 100;
        MindscapeDatabase.SaveJournal(chapter, episode, EpisodeReflection.Prompt(chapter, episode), text);
    }
    // Offline, Chapter 2 reflections written before they moved to the database are still shown.
    static string JournalEntry(int chapter, int episode)
    {
        string entry = MindscapeDatabase.LatestJournal(chapter, episode);
        return entry.Length > 0 || MindscapeCloud.IsSignedIn || chapter != 2 ? entry : PlayerPrefs.GetString("Mindscape.Reflection." + (episode - 1), "");
    }

    // ---------------------------------------------------------------- options
    static readonly Dictionary<string, float> VolumeDefaults = new Dictionary<string, float>
    { { "Mindscape.Master", .8f }, { "Mindscape.Music", .65f }, { "Mindscape.SFX", .65f }, { "Mindscape.MenuShade", 0 } };
    static readonly string[] KeyActions = { "Move", "Move", "Move", "Move", "Run", "FreeLook", "OpenTasks", "TogglePause" };
    static readonly string[] KeyParts = { "up", "down", "left", "right", null, null, null, null };

    void BindOptions()
    {
        Tabs("page-options", 6, tab => { if (tab == 2) RefreshMusic(); if (tab == 3) RefreshKeys(); if (tab == 5) RefreshAccountPanel(); });
        On("options-sign-out", () => MindscapeCloud.SignOut());
        On("options-log-in", () => MindscapeCloud.SignOut());   // ends offline play, so the login page comes back
        On("options-delete", () => { if (MindscapeCloud.IsSignedIn) OpenModal(AccountStep.DeleteConfirm); });
        RefreshAccountPanel();
        var page = pages.ContainsKey("options") ? pages["options"] : root;
        // Every slider is named by the setting it saves; General and Audio share the volume ones.
        page.Query<Slider>().ForEach(slider =>
        {
            string key = slider.name;
            float saved = Mathf.Clamp01(PlayerPrefs.GetFloat(key, VolumeDefaults.TryGetValue(key, out var d) ? d : .65f));
            SetVolumeUI(key, saved);
            if (Application.isPlaying) ApplyVolume(key, saved);
            slider.RegisterValueChangedCallback(evt => { PlayerPrefs.SetFloat(key, evt.newValue); PlayerPrefs.Save(); SetVolumeUI(key, evt.newValue); ApplyVolume(key, evt.newValue); });
        });
        var mobile = root.Q<Toggle>("Mindscape.MobileControls");
        if (mobile != null)
        {
            mobile.SetValueWithoutNotify(PlayerPrefs.GetInt("Mindscape.MobileControls", MobileControls.DefaultOn ? 1 : 0) == 1);
            mobile.RegisterValueChangedCallback(evt => { PlayerPrefs.SetInt("Mindscape.MobileControls", evt.newValue ? 1 : 0); PlayerPrefs.Save(); ApplyMobileMode(); });
        }
        var firstPerson = root.Q<Toggle>("Mindscape.FirstPerson");
        if (firstPerson != null)
        {
            firstPerson.SetValueWithoutNotify(PlayerPrefs.GetInt(PlayerMovement.FirstPersonKey, 0) == 1);
            firstPerson.RegisterValueChangedCallback(evt => { PlayerPrefs.SetInt(PlayerMovement.FirstPersonKey, evt.newValue ? 1 : 0); PlayerPrefs.Save(); });
        }
        foreach (var reset in new[] { "reset-audio", "reset-audio-general" })
            On(reset, () =>
            {
                foreach (var key in new[] { "Mindscape.Master", "Mindscape.Music", "Mindscape.SFX" }) { PlayerPrefs.SetFloat(key, VolumeDefaults[key]); SetVolumeUI(key, VolumeDefaults[key]); ApplyVolume(key, VolumeDefaults[key]); }
                PlayerPrefs.Save();
            });
        var quality = root.Q<DropdownField>("quality");
        if (quality != null)
        {
            var levels = new List<string>(QualitySettings.names);
            quality.choices = levels; quality.SetValueWithoutNotify(levels[Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, levels.Count - 1)]);
            quality.RegisterValueChangedCallback(evt => GraphicsPrefs.Set(GraphicsPrefs.QualityKey, levels.IndexOf(evt.newValue)));
        }
        BindChoice("texture-quality", GraphicsPrefs.TextureNames, GraphicsPrefs.Texture, GraphicsPrefs.TextureKey);
        BindChoice("frame-rate", GraphicsPrefs.FrameRateNames, GraphicsPrefs.FrameRate, GraphicsPrefs.FrameRateKey);
        BindChoice("display-mode", GraphicsPrefs.DisplayNames, GraphicsPrefs.Display, GraphicsPrefs.DisplayKey);
        root.Q("display-mode")?.EnableInClassList("hidden", !GraphicsPrefs.DesktopDisplay);
        var vsync = root.Q<Toggle>(GraphicsPrefs.VSyncKey);
        if (vsync != null)
        {
            vsync.SetValueWithoutNotify(GraphicsPrefs.VSync);
            vsync.RegisterValueChangedCallback(evt => GraphicsPrefs.Set(GraphicsPrefs.VSyncKey, evt.newValue ? 1 : 0));
        }
        // Now playing: skip to the next track, or stop and start the music. MusicManager remembers both choices.
        On("music-next", () => { MusicManager.Next(); RefreshMusic(); });
        On("music-toggle", () => { if (MusicManager.IsStopped) MusicManager.Play(); else MusicManager.Stop(); RefreshMusic(); });
        RefreshMusic();
        for (int i = 0; i < KeyActions.Length; i++) { int index = i; On("key-" + i, () => { var (action, binding) = KeyBinding(index); BeginRebind(action, binding, root.Q<Button>("key-" + index)); }); }
        On("keys-reset", () => { KeyBindings.ResetToDefaults(); RefreshKeys(); });
        RefreshKeys();
    }
    void BindChoice(string name, string[] names, int current, string key)
    {
        var field = root.Q<DropdownField>(name); if (field == null) return;
        var choices = new List<string>(names);
        field.choices = choices; field.SetValueWithoutNotify(choices[current]);
        field.RegisterValueChangedCallback(evt => GraphicsPrefs.Set(key, choices.IndexOf(evt.newValue)));
    }
    void SetVolumeUI(string key, float value)
    {
        root.Query<Slider>(key).ForEach(slider =>
        {
            slider.SetValueWithoutNotify(value);
            var label = slider.parent?.Q<Label>(className: "setting-value"); if (label != null) label.text = Mathf.RoundToInt(value * 100) + "%";
        });
    }
    void ApplyVolume(string key, float value)
    {
        if (key == "Mindscape.Master" && Application.isPlaying) AudioListener.volume = value;
        if (key == "Mindscape.MenuShade" && brightness != null) brightness.style.opacity = value * .45f;
    }
    void RefreshMusic()
    {
        var title = root.Q<Label>("music-title"); var toggle = root.Q<Button>("music-toggle");
        if (title != null) title.text = MusicManager.CurrentTitle + (MusicManager.IsStopped ? "  (stopped)" : "");
        if (toggle != null) toggle.text = MusicManager.IsStopped ? "Play" : "Stop";
    }
    (InputAction action, int binding) KeyBinding(int index)
    {
        var action = KeyBindings.Gameplay?.FindAction(KeyActions[index]); int binding = 0;
        if (KeyParts[index] != null && action != null) { binding = -1; for (int k = 0; k < action.bindings.Count; k++) if (action.bindings[k].name == KeyParts[index]) binding = k; }
        return (action, binding);
    }
    void RefreshKeys()
    {
        for (int i = 0; i < KeyActions.Length; i++)
        {
            var button = root.Q<Button>("key-" + i); if (button == null) continue;
            var (action, binding) = KeyBinding(i); button.text = Binding(action, binding);
        }
    }
    static string Binding(InputAction action, int index) => action != null && index >= 0 && index < action.bindings.Count ? action.bindings[index].ToDisplayString() : "Unavailable";
    void BeginRebind(InputAction action, int index, Button button)
    {
        if (!Application.isPlaying || action == null || index < 0 || button == null) return;
        rebind?.Cancel(); rebindingAction = action; actionWasEnabled = action.enabled; action.Disable(); button.text = "Press a key…";
        rebind = action.PerformInteractiveRebinding(index).WithControlsExcluding("Mouse").WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op => { KeyBindings.SaveOverrides(); FinishRebind(op, button, action, index); })
            .OnCancel(op => FinishRebind(op, button, action, index)); rebind.Start();
    }
    void FinishRebind(InputActionRebindingExtensions.RebindingOperation op, Button button, InputAction action, int index)
    { op.Dispose(); rebind = null; if (actionWasEnabled) rebindingAction.Enable(); button.text = Binding(action, index); }

    // ---------------------------------------------------------------- guide, resources, quit
    void BindGuide()
    {
        Tabs("page-guide", 4);
        On("guide-play-tutorial", () => StartScene(tutorialScene, 1, 1, true));
    }
    void BindResources()
    {
        Tabs("page-resources", 5);
        // A link button opens the address in its tooltip (edit it in UI Builder's Inspector).
        foreach (var name in new[] { "link-hotline", "link-helpline", "link-ncmh", "link-provider-ncmh", "link-provider-intouch", "link-provider-taguig", "link-provider-pmha" })
        {
            var link = root.Q<Button>(name);
            On(link, () => { if (link != null && !string.IsNullOrEmpty(link.tooltip)) Application.OpenURL(link.tooltip); });
        }
        On("resources-journal", () => ShowPage("journal"));
    }
    void BindQuit()
    {
        On("quit-cancel", () => ShowPage("home"));
        On("quit-confirm", () =>
        {
            PlayerPrefs.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            Notify("You can now close this browser tab. Your changes are saved.");
#else
            Application.Quit();
#endif
        });
    }
    void StartScene(string scene, int chapter, int episode, bool newGame)
    {
        if (!Application.isPlaying || loading) return;
        if (!Application.CanStreamedLevelBeLoaded(scene)) { Notify("This episode is not included in the current build."); return; }
        // Existing save convention: episode 1 = tutorial, episode 2 = first story episode.
        if (newGame) PlayerPrefs.SetInt(MindscapeProgress.ChapterUnlockedKey, Mathf.Max(chapter, PlayerPrefs.GetInt(MindscapeProgress.ChapterUnlockedKey, 1)));
        PlayerPrefs.SetInt(MindscapeProgress.ChapterKey, chapter); PlayerPrefs.SetInt(MindscapeProgress.EpisodeKey, episode); PlayerPrefs.Save();
        loading = EpisodeLoadingScreen.Load(scene, Mathf.Max(1, episode - 1), episode == 1 ? 1 : chapter);
    }
}

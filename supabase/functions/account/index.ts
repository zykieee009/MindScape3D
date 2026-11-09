// Account gateway for the Unity game: sign-up and password reset confirmed with an emailed OTP, and login by
// username or email. It sits in front of Supabase Auth (GoTrue) so the game never learns which email belongs to a
// username. Deployed with verify_jwt = false (the game calls it with its publishable key); every action does its
// own checks. Players are created by the ensure_profile() RPC once an email is confirmed.
// Deleting an account archives it for 30 days (archive_account); until then it can be recovered from the login
// screen with an emailed code (recover_start -> recover_verify -> recover_account). A daily pg_cron job then deletes it.
// The "your account was deleted" email goes through Resend when RESEND_API_KEY and MAIL_FROM are set as secrets.
// Sign-up takes only Gmail and Yahoo addresses, refuses a username or email that is already used, and needs a strong
// password (8+ characters with upper and lower case, a number and a symbol); check_available lets the form say so early.
// "Continue with Google" (migration mindscape_google_and_unique_email): the game opens GET /account/google/start/<state>
// in the browser; this function starts a PKCE sign-in with Supabase Auth's Google provider, Google sends the player
// back to GET /account/google/done/<state>, the code is exchanged here and the session parked in google_handoffs,
// and the game collects it with google_poll. A Google player without a profile picks a username (google_username).
// Dashboard: enable the Google provider, and add FN_URL + "/google/done/**" to Auth > URL Configuration > Redirect URLs.
// Started with ?app=1 (Android Custom Tab, macOS sign-in sheet), the last page redirects to mindscape://google-done,
// which closes the tab or sheet and brings the game back (migration mindscape_google_app_return).

const SUPABASE_URL = Deno.env.get("SUPABASE_URL")!;
const ANON_KEY = Deno.env.get("SUPABASE_ANON_KEY")!;
const SERVICE_KEY = Deno.env.get("SUPABASE_SERVICE_ROLE_KEY")!;
const RESEND_KEY = Deno.env.get("RESEND_API_KEY") ?? "";
const MAIL_FROM = Deno.env.get("MAIL_FROM") ?? "";

const FN_URL = `${SUPABASE_URL}/functions/v1/account`;

const USERNAME = /^[A-Za-z0-9_.]{3,24}$/;
const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
// Gmail (and its old googlemail.com) and Yahoo, including Yahoo's country domains such as yahoo.com.ph or yahoo.co.uk.
const EMAIL_DOMAIN = /@(gmail\.com|googlemail\.com|yahoo\.com|yahoo\.com\.[a-z]{2}|yahoo\.co\.[a-z]{2}|yahoo\.[a-z]{2})$/;
// At least one lower case letter, one upper case letter, one number and one symbol, 8-72 characters.
const STRONG_PASSWORD = /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{8,72}$/;
const STATE = /^[A-Za-z0-9_-]{32,128}$/;
const CODE = /^\d{6,10}$/;
const SESSION_EXPIRED = "28000";
const ARCHIVED = "account_archived";

const cors = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, x-client-info, apikey, content-type",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
};

class Fail extends Error {
  constructor(message: string, public code = "account_error", public extra: Record<string, unknown> = {}) {
    super(message);
  }
}

const reply = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { ...cors, "Content-Type": "application/json" } });

type Reply = { ok: boolean; status: number; json: any };

async function send(url: string, init: RequestInit): Promise<Reply> {
  const response = await fetch(url, init);
  const text = await response.text();
  let json: any = {};
  try { json = text ? JSON.parse(text) : {}; } catch { /* not JSON */ }
  return { ok: response.ok, status: response.status, json };
}

function gotrue(path: string, body: unknown, bearer = ANON_KEY, method = "POST") {
  return send(`${SUPABASE_URL}/auth/v1/${path}`, {
    method,
    headers: { apikey: ANON_KEY, Authorization: `Bearer ${bearer}`, "Content-Type": "application/json" },
    body: method === "GET" ? undefined : JSON.stringify(body),
  });
}

// The RPCs run as the caller (ensure_profile) or as the service role (the username lookups).
function rpc(name: string, args: unknown, bearer: string, apikey = ANON_KEY) {
  return send(`${SUPABASE_URL}/rest/v1/rpc/${name}`, {
    method: "POST",
    headers: { apikey, Authorization: `Bearer ${bearer}`, "Content-Type": "application/json" },
    body: JSON.stringify(args),
  });
}
const service = (name: string, args: unknown) => rpc(name, args, SERVICE_KEY, SERVICE_KEY);

const text = (value: unknown) => (typeof value === "string" ? value.trim() : "");
const problem = (r: Reply) => r.json.msg ?? r.json.message ?? r.json.error_description ?? "Something went wrong. Try again.";

const PASSWORD_RULE = "Use 8-72 characters with an upper case letter, a lower case letter, a number and a symbol (like ! or #).";
function checkPassword(password: string) {
  if (!STRONG_PASSWORD.test(password)) throw new Fail(PASSWORD_RULE, "weak_password");
}

// Sign-up checks. Each returns the message to show, or null when the value is fine.
async function usernameError(username: string) {
  if (!USERNAME.test(username)) return "Usernames are 3-24 characters: letters, numbers, _ or .";
  const taken = await service("username_taken", { p_username: username });
  return taken.ok && taken.json === true ? "That username is already taken. Try another one." : null;
}
async function emailError(email: string) {
  if (!EMAIL.test(email)) return "Enter a valid email address.";
  if (!EMAIL_DOMAIN.test(email)) return "Only Gmail and Yahoo addresses are accepted (for example name@gmail.com or name@yahoo.com).";
  const taken = await service("email_taken", { p_email: email });
  return taken.ok && taken.json === true ? "An account already uses this email. Log in instead, or choose \"Forgot password?\"." : null;
}
function checkCode(code: string) {
  if (!CODE.test(code)) throw new Fail("Enter the code from your email.");
}
const bearerOf = (request: Request) => (request.headers.get("Authorization") ?? "").replace(/^Bearer\s+/i, "");

// "1 December 2026", the way the emails and messages write dates.
const day = (iso: string) => new Date(iso).toLocaleDateString("en-GB", { day: "numeric", month: "long", year: "numeric", timeZone: "UTC" });

// The signed-in account's {username, status, archived_at, delete_after}, or null before it has a profile.
async function stateOf(accessToken: string) {
  const r = await rpc("account_state", {}, accessToken);
  return r.ok && r.json && typeof r.json === "object" ? r.json : null;
}

function archivedFail(state: any) {
  return new Fail(
    `This account was deleted on ${day(state.archived_at)}. You can recover it until ${day(state.delete_after)}: choose "Recover a deleted account" on the login screen.`,
    ARCHIVED, { delete_after: state.delete_after });
}

async function mail(to: string, subject: string, text: string) {
  if (!RESEND_KEY || !MAIL_FROM) { console.warn("account: RESEND_API_KEY / MAIL_FROM not set, email skipped:", subject); return false; }
  const r = await send("https://api.resend.com/emails", {
    method: "POST",
    headers: { Authorization: `Bearer ${RESEND_KEY}`, "Content-Type": "application/json" },
    body: JSON.stringify({ from: MAIL_FROM, to: [to], subject, text }),
  });
  if (!r.ok) console.error("account: email failed", r.status, r.json);
  return r.ok;
}

// A username or an email -> the account's email, or null when there is none.
async function emailFor(identifier: string): Promise<string | null> {
  if (identifier.includes("@")) return identifier.toLowerCase();
  if (!USERNAME.test(identifier)) return null;
  const r = await service("email_for_username", { p_username: identifier });
  return r.ok && typeof r.json === "string" ? r.json : null;
}

// Makes sure the confirmed user has a player row, and returns the session the game stores.
async function sessionFor(auth: any, cleanupOnFailure = false) {
  const profile = await rpc("ensure_profile", {}, auth.access_token);
  if (!profile.ok) {
    // A username lost in a race: remove the confirmed-but-profileless user so the email can sign up again.
    if (cleanupOnFailure && auth.user?.id) {
      await send(`${SUPABASE_URL}/auth/v1/admin/users/${auth.user.id}`, {
        method: "DELETE",
        headers: { apikey: SERVICE_KEY, Authorization: `Bearer ${SERVICE_KEY}` },
      });
    }
    throw new Fail(profile.json.message ?? "Could not finish creating your account.");
  }
  return {
    access_token: auth.access_token,
    refresh_token: auth.refresh_token,
    expires_in: auth.expires_in,
    username: profile.json.username,
  };
}

// ---------------------------------------------------------------- Continue with Google

function handoffs(query: string, init: RequestInit) {
  return send(`${SUPABASE_URL}/rest/v1/google_handoffs${query}`, {
    ...init,
    headers: { apikey: SERVICE_KEY, Authorization: `Bearer ${SERVICE_KEY}`, "Content-Type": "application/json", Prefer: "return=minimal" },
  });
}

const base64url = (bytes: Uint8Array) => btoa(String.fromCharCode(...bytes)).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");

// A free username made from the email's name part: "maria.santos" for maria.santos@gmail.com, with digits added if taken.
async function suggestUsername(email: string) {
  let base = email.split("@")[0].replace(/[^A-Za-z0-9_.]/g, "").slice(0, 20);
  if (base.length < 3) base = "player";
  if (!(await usernameError(base))) return base;
  for (let i = 0; i < 5; i++) {
    const name = `${base}${Math.floor(100 + Math.random() * 9900)}`;
    if (!(await usernameError(name))) return name;
  }
  return "";
}

// The browser half. Supabase serves function GET replies as plain text, so the pages are plain sentences.
const page = (message: string, status = 200) =>
  new Response(message, { status, headers: { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store" } });

async function google(request: Request) {
  const url = new URL(request.url);
  const match = url.pathname.match(/\/google\/(start|done)\/([A-Za-z0-9_-]{32,128})\/?$/);
  if (!match) return page("Mindscape: this link is not valid.", 404);
  const [, step, state] = match;

  if (step === "start") {
    // Old, abandoned sign-ins are cleared out as new ones begin.
    await handoffs(`?created_at=lt.${new Date(Date.now() - 15 * 60_000).toISOString()}`, { method: "DELETE" });
    const verifier = base64url(crypto.getRandomValues(new Uint8Array(32)));
    const challenge = base64url(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(verifier))));
    const returnApp = url.searchParams.get("app") === "1";
    const saved = await handoffs("", { method: "POST", body: JSON.stringify({ state, verifier, return_app: returnApp }) });
    if (!saved.ok) {
      // "Open browser again" reuses the state: restart the same sign-in unless it already finished.
      const found = await handoffs(`?state=eq.${state}&select=session`, { method: "GET" });
      const row = found.ok && Array.isArray(found.json) ? found.json[0] : null;
      if (!row) return page("Mindscape: this sign-in has expired. Go back to the game and choose Continue with Google again.", 410);
      if (row.session) return page("Mindscape: you are signed in. Go back to the game; you can close this tab.");
      await handoffs(`?state=eq.${state}`, { method: "PATCH", body: JSON.stringify({ verifier, error: null, return_app: returnApp }) });
    }
    const authorize = `${SUPABASE_URL}/auth/v1/authorize?provider=google` +
      `&redirect_to=${encodeURIComponent(`${FN_URL}/google/done/${state}`)}` +
      `&code_challenge=${challenge}&code_challenge_method=s256`;
    return new Response(null, { status: 302, headers: { Location: authorize, "Cache-Control": "no-store" } });
  }

  const found = await handoffs(`?state=eq.${state}&select=verifier,session,return_app`, { method: "GET" });
  const row = found.ok && Array.isArray(found.json) ? found.json[0] : null;
  if (!row) return page("Mindscape: this sign-in has expired. Go back to the game and choose Continue with Google again.", 410);
  // In the app's own tab or sheet, the last page is the game itself; in a browser, a sentence.
  const finish = (message: string, ok: boolean, status = 200) => row.return_app
    ? new Response(null, { status: 302, headers: { Location: `mindscape://google-done?result=${ok ? "ok" : "error"}`, "Cache-Control": "no-store" } })
    : page(message, status);
  if (row.session) return finish("Mindscape: you are signed in. Go back to the game; you can close this tab.", true);
  const fail = async (message: string) => {
    await handoffs(`?state=eq.${state}`, { method: "PATCH", body: JSON.stringify({ error: message }) });
    return finish(`Mindscape: ${message} Go back to the game; you can close this tab.`, false, 400);
  };
  const code = url.searchParams.get("code");
  if (!code) {
    const why = url.searchParams.get("error_description");
    return await fail(why ? `Google sign-in did not finish (${why}).` : "Google sign-in was cancelled.");
  }
  const r = await gotrue("token?grant_type=pkce", { auth_code: code, code_verifier: row.verifier });
  if (!r.ok || !r.json.access_token) return await fail("Google sign-in did not finish. Try again.");
  const session = { access_token: r.json.access_token, refresh_token: r.json.refresh_token, expires_in: r.json.expires_in };
  await handoffs(`?state=eq.${state}`, { method: "PATCH", body: JSON.stringify({ session }) });
  return finish("Mindscape: you are signed in with Google. Go back to the game; you can close this tab.", true);
}

const actions: Record<string, (body: any, request: Request) => Promise<unknown>> = {
  // Starts an account: checks the details and has Supabase Auth email a code.
  async signup(body) {
    const username = text(body.username), email = text(body.email).toLowerCase(), password = String(body.password ?? "");
    const badName = await usernameError(username);
    if (badName) throw new Fail(badName, "username_unavailable");
    const badEmail = await emailError(email);
    if (badEmail) throw new Fail(badEmail, "email_unavailable");
    checkPassword(password);
    const r = await gotrue("signup", { email, password, data: { username } });
    if (!r.ok) throw new Fail(r.status === 429 ? "Too many emails sent. Wait a minute and try again." : problem(r));
    return { message: "Code sent." };
  },

  // The sign-up form asks while the player types: {username_error, email_error}, each null when fine.
  // Only the fields that were sent are checked.
  async check_available(body) {
    const username = text(body.username), email = text(body.email).toLowerCase();
    return {
      username_error: username ? await usernameError(username) : null,
      email_error: email ? await emailError(email) : null,
    };
  },

  async resend_signup(body) {
    const email = text(body.email).toLowerCase();
    if (!EMAIL.test(email)) throw new Fail("Enter a valid email address.");
    const r = await gotrue("resend", { type: "signup", email });
    if (!r.ok) throw new Fail(r.status === 429 ? "Wait a minute before asking for another code." : problem(r));
    return { message: "Code sent." };
  },

  // The emailed code matched: the account is confirmed, so create the player and sign in.
  async verify_signup(body) {
    const email = text(body.email).toLowerCase(), code = text(body.code);
    checkCode(code);
    const r = await gotrue("verify", { type: "signup", email, token: code });
    if (!r.ok || !r.json.access_token) throw new Fail("That code is wrong or has expired.");
    return { session: await sessionFor(r.json, true) };
  },

  async login(body) {
    const identifier = text(body.identifier), password = String(body.password ?? "");
    if (!identifier || !password) throw new Fail("Enter your username or email, and your password.");
    const email = await emailFor(identifier);
    const wrong = new Fail("Incorrect username/email or password.");
    if (!email) throw wrong;
    const r = await gotrue("token?grant_type=password", { email, password });
    if (!r.ok) {
      if (r.json.error_code === "email_not_confirmed") {
        throw new Fail("Verify your email first. We can send you a new code.", "email_not_confirmed", { email });
      }
      throw wrong;
    }
    const state = await stateOf(r.json.access_token);
    if (state?.status === "archived") {
      await gotrue("logout", {}, r.json.access_token);
      throw archivedFail(state);
    }
    return { session: await sessionFor(r.json) };
  },

  // Emails a reset code when the username or email belongs to an account. The reply never says whether it does.
  async recover(body) {
    const identifier = text(body.identifier);
    if (!identifier) throw new Fail("Enter your username or email.");
    const email = await emailFor(identifier);
    if (email) await gotrue("recover", { email });
    return { message: "If an account exists, a code is on its way." };
  },

  // The code matched: return a short-lived session that is only good for choosing a new password.
  async verify_recovery(body) {
    const identifier = text(body.identifier), code = text(body.code);
    checkCode(code);
    const email = await emailFor(identifier);
    const wrong = new Fail("That code is wrong or has expired.");
    if (!email) throw wrong;
    const r = await gotrue("verify", { type: "recovery", email, token: code });
    if (!r.ok || !r.json.access_token) throw wrong;
    const state = await stateOf(r.json.access_token);
    if (state?.status === "archived") {
      await gotrue("logout", {}, r.json.access_token);
      throw archivedFail(state);
    }
    return { session: { access_token: r.json.access_token, refresh_token: r.json.refresh_token, expires_in: r.json.expires_in } };
  },

  // Authorization: Bearer <access token from verify_recovery>.
  async set_password(body, request) {
    const bearer = bearerOf(request);
    const password = String(body.password ?? "");
    checkPassword(password);
    if (!bearer || bearer === ANON_KEY) throw new Fail("Your reset code has expired. Start again.", SESSION_EXPIRED);
    const r = await gotrue("user", { password }, bearer, "PUT");
    if (!r.ok) throw new Fail(r.status === 401 ? "Your reset code has expired. Start again." : problem(r), r.status === 401 ? SESSION_EXPIRED : "account_error");
    const profile = await rpc("ensure_profile", {}, bearer);
    if (!profile.ok) throw new Fail(profile.json.message ?? "Could not finish signing you in.");
    return { username: profile.json.username };
  },

  async refresh(body) {
    const refreshToken = text(body.refresh_token);
    const r = await gotrue("token?grant_type=refresh_token", { refresh_token: refreshToken });
    if (!r.ok || !r.json.access_token) throw new Fail("Your session has expired. Please log in again.", SESSION_EXPIRED);
    return { session: { access_token: r.json.access_token, refresh_token: r.json.refresh_token, expires_in: r.json.expires_in } };
  },

  async logout(_body, request) {
    const bearer = bearerOf(request);
    if (bearer && bearer !== ANON_KEY) await gotrue("logout", {}, bearer);
    return { message: "Signed out." };
  },

  // Authorization: Bearer <the player's access token>. Archives the account for 30 days, signs it out everywhere
  // and emails the player how long they have to recover it.
  async delete_account(_body, request) {
    const bearer = bearerOf(request);
    if (!bearer || bearer === ANON_KEY) throw new Fail("Please log in again.", SESSION_EXPIRED);
    const user = await gotrue("user", {}, bearer, "GET");
    if (!user.ok || !user.json.email) throw new Fail("Your session has expired. Please log in again.", SESSION_EXPIRED);
    const r = await rpc("archive_account", {}, bearer);
    if (!r.ok) throw new Fail(r.json.message ?? "Could not delete your account. Try again.");
    const { username, delete_after } = r.json;
    await gotrue("logout?scope=global", {}, bearer);
    const emailed = await mail(user.json.email, "Your Mindscape account was deleted",
      `Hi ${username},\n\n` +
      `Your Mindscape account was deleted and you have been signed out.\n\n` +
      `You still have 30 days to change your mind. Until ${day(delete_after)}, open Mindscape, choose ` +
      `"Recover a deleted account" on the login screen and enter this email address. We will send you a code, and ` +
      `your journal and progress come back with your account.\n\n` +
      `After ${day(delete_after)}, your account and everything saved with it are permanently deleted. You do not need to do anything.\n\n` +
      `If you did not delete your account, recover it and choose a new password.\n\n- Mindscape`);
    return { delete_after, emailed };
  },

  // Emails a recovery code when the email belongs to a deleted account that is still within its 30 days.
  // Like "recover", the reply never says whether it does.
  async recover_start(body) {
    const email = text(body.email).toLowerCase();
    if (!EMAIL.test(email)) throw new Fail("Enter a valid email address.");
    const found = await service("recoverable_email", { p_email: email });
    if (found.ok && found.json === true) {
      const r = await gotrue("recover", { email });
      if (!r.ok && r.status === 429) throw new Fail("Wait a minute before asking for another code.");
    }
    return { message: "If a deleted account uses this email, a code is on its way." };
  },

  // The code matched: return the recovery session and what the player is about to recover, so the game can ask
  // "Are you sure?" before anything changes.
  async recover_verify(body) {
    const email = text(body.email).toLowerCase(), code = text(body.code);
    checkCode(code);
    const wrong = new Fail("That code is wrong or has expired.");
    if (!EMAIL.test(email)) throw wrong;
    const r = await gotrue("verify", { type: "recovery", email, token: code });
    if (!r.ok || !r.json.access_token) throw wrong;
    const state = await stateOf(r.json.access_token);
    if (state?.status !== "archived" || new Date(state.delete_after) <= new Date()) {
      await gotrue("logout", {}, r.json.access_token);
      throw new Fail("There is no deleted account to recover for this email.");
    }
    return {
      session: { access_token: r.json.access_token, refresh_token: r.json.refresh_token, expires_in: r.json.expires_in },
      username: state.username, archived_at: state.archived_at, delete_after: state.delete_after,
    };
  },

  // "Continue with Google", step 2: the game asks every couple of seconds until the browser part is done.
  // {pending: true} while waiting; then either {session} (signed in) or {need_username, suggestion, session} for a new
  // Google player, who chooses a username next (google_username).
  async google_poll(body) {
    const state = text(body.state);
    if (!STATE.test(state)) throw new Fail("Google sign-in expired. Try again.", "google_expired");
    const r = await handoffs(`?state=eq.${state}&select=session,error`, { method: "GET" });
    const row = r.ok && Array.isArray(r.json) ? r.json[0] : null;
    if (!row) throw new Fail("Google sign-in expired. Try again.", "google_expired");
    if (!row.session && !row.error) return { pending: true };
    await handoffs(`?state=eq.${state}`, { method: "DELETE" });
    if (row.error) throw new Fail(row.error, "google_failed");
    const auth = row.session;
    const state0 = await stateOf(auth.access_token);
    if (state0?.status === "archived") { await gotrue("logout", {}, auth.access_token); throw archivedFail(state0); }
    if (state0) return { session: await sessionFor(auth) };
    // No player yet. Use a username from the metadata if it is free, otherwise ask for one.
    const user = await gotrue("user", {}, auth.access_token, "GET");
    const wanted = text(user.json?.user_metadata?.username);
    if (wanted && !(await usernameError(wanted))) return { session: await sessionFor(auth) };
    return {
      need_username: true,
      suggestion: await suggestUsername(text(user.json?.email)),
      session: { access_token: auth.access_token, refresh_token: auth.refresh_token, expires_in: auth.expires_in },
    };
  },

  // Authorization: Bearer <access token from google_poll>. Saves the chosen username and creates the player.
  async google_username(body, request) {
    const bearer = bearerOf(request);
    if (!bearer || bearer === ANON_KEY) throw new Fail("Google sign-in expired. Try again.", SESSION_EXPIRED);
    const username = text(body.username);
    const bad = await usernameError(username);
    if (bad) throw new Fail(bad, "username_unavailable");
    const r = await gotrue("user", { data: { username } }, bearer, "PUT");
    if (!r.ok) throw new Fail(r.status === 401 ? "Google sign-in expired. Try again." : problem(r), r.status === 401 ? SESSION_EXPIRED : "account_error");
    const profile = await rpc("ensure_profile", {}, bearer);
    if (!profile.ok) throw new Fail(profile.json.message ?? "Could not finish creating your account.");
    return { username: profile.json.username };
  },

  // Authorization: Bearer <access token from recover_verify>. Sets the new password, then brings the account back.
  async recover_account(body, request) {
    const bearer = bearerOf(request);
    const password = String(body.password ?? "");
    checkPassword(password);
    const expired = new Fail("Your recovery code has expired. Start again.", SESSION_EXPIRED);
    if (!bearer || bearer === ANON_KEY) throw expired;
    const r = await gotrue("user", { password }, bearer, "PUT");
    if (!r.ok) { if (r.status === 401) throw expired; throw new Fail(problem(r)); }
    const restored = await rpc("restore_account", {}, bearer);
    if (!restored.ok) throw new Fail(restored.json.message ?? "Could not recover your account. Try again.");
    const profile = await rpc("ensure_profile", {}, bearer);
    if (!profile.ok) throw new Fail(profile.json.message ?? "Could not finish signing you in.");
    return { username: profile.json.username };
  },
};

Deno.serve(async (request) => {
  if (request.method === "OPTIONS") return new Response(null, { status: 204, headers: cors });
  if (request.method === "GET") {
    try { return await google(request); }
    catch (error) { console.error(error); return page("Mindscape: something went wrong. Go back to the game and try again.", 500); }
  }
  if (request.method !== "POST") return reply({ code: "account_error", message: "Use POST." }, 405);
  try {
    const body = await request.json().catch(() => ({}));
    const handler = actions[text(body.action)];
    if (!handler) throw new Fail("Unknown action.");
    return reply(await handler(body, request));
  } catch (error) {
    if (error instanceof Fail) return reply({ code: error.code, message: error.message, ...error.extra }, 400);
    console.error(error);
    return reply({ code: "account_error", message: "Something went wrong. Try again." }, 500);
  }
});

// Mindscape · "Continue with Google" on macOS: Apple's sign-in sheet (ASWebAuthenticationSession) over the game window.
// The sheet closes by itself when the account function's last page goes to mindscape://google-done. The game polls
// MindscapeAuthState() (and the server, for the session); nothing calls back into C#.
// Build: NativePlugins/macOS/build.sh -> Assets/Plugins/macOS/MindscapeAuth.bundle
#import <AppKit/AppKit.h>
#import <AuthenticationServices/AuthenticationServices.h>

// 0 idle, 1 open, 2 finished (reached mindscape://), 3 cancelled by the player, 4 failed
static int state = 0;
static ASWebAuthenticationSession *session API_AVAILABLE(macos(10.15));

API_AVAILABLE(macos(10.15))
@interface MindscapeAuthAnchor : NSObject <ASWebAuthenticationPresentationContextProviding>
@end
@implementation MindscapeAuthAnchor
- (ASPresentationAnchor)presentationAnchorForWebAuthenticationSession:(ASWebAuthenticationSession *)s
{
    NSWindow *w = NSApp.keyWindow ?: NSApp.mainWindow;
    return w ?: NSApp.windows.firstObject;
}
@end
static id anchor;

int MindscapeAuthAvailable(void)
{
    if (@available(macOS 10.15, *)) return 1;
    return 0;
}

// Opens the sheet at url; it closes when a page goes to <scheme>://... Returns 1 when it opened.
int MindscapeAuthStart(const char *url, const char *scheme)
{
    if (@available(macOS 10.15, *))
    {
        NSURL *address = [NSURL URLWithString:[NSString stringWithUTF8String:url]];
        NSString *callback = [NSString stringWithUTF8String:scheme];
        if (address == nil) return 0;
        __block int opened = 0;
        void (^open)(void) = ^{
            if (session != nil) [session cancel];
            session = [[ASWebAuthenticationSession alloc] initWithURL:address callbackURLScheme:callback
                completionHandler:^(NSURL *returned, NSError *error) {
                    if (returned != nil) state = 2;
                    else if (error != nil && error.domain == ASWebAuthenticationSessionErrorDomain && error.code == ASWebAuthenticationSessionErrorCodeCanceledLogin) state = 3;
                    else state = 4;
                    session = nil;
                }];
            if (anchor == nil) anchor = [MindscapeAuthAnchor new];
            session.presentationContextProvider = anchor;
            session.prefersEphemeralWebBrowserSession = NO;   // remembers the Google account, like the browser
            state = 1;
            opened = [session start] ? 1 : 0;
            if (!opened) { state = 4; session = nil; }
        };
        if ([NSThread isMainThread]) open(); else dispatch_sync(dispatch_get_main_queue(), open);
        return opened;
    }
    return 0;
}

int MindscapeAuthState(void) { return state; }

void MindscapeAuthCancel(void)
{
    if (@available(macOS 10.15, *))
    {
        void (^close)(void) = ^{ if (session != nil) [session cancel]; session = nil; state = 0; };
        if ([NSThread isMainThread]) close(); else dispatch_async(dispatch_get_main_queue(), close);
    }
}

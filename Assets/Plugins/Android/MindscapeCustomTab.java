package com.mindscape.auth;

import android.app.Activity;
import android.content.Intent;
import android.content.pm.PackageManager;
import android.content.pm.ResolveInfo;
import android.net.Uri;
import android.os.Bundle;
import java.util.List;

// Mindscape · "Continue with Google" on Android: the sign-in opens in a Chrome Custom Tab (a browser sheet over the game,
// with the phone's own Google accounts) instead of a separate browser app. The account function's last page goes to
// mindscape://google-done, which AndroidManifest.xml routes back to the game; that brings the game to the front and
// closes the tab. Built from the Custom Tabs intent extras directly, so no androidx.browser dependency is needed.
public final class MindscapeCustomTab
{
    static final String SESSION = "android.support.customtabs.extra.SESSION";
    static final String TOOLBAR_COLOR = "android.support.customtabs.extra.TOOLBAR_COLOR";
    static final String TITLE = "android.support.customtabs.extra.TITLE_VISIBILITY";
    static final String SERVICE = "android.support.customtabs.action.CustomTabsService";

    // Returns false only if nothing could open the page.
    public static boolean open(Activity activity, String url, int toolbarColor)
    {
        try
        {
            Intent intent = new Intent(Intent.ACTION_VIEW, Uri.parse(url));
            Bundle extras = new Bundle();
            extras.putBinder(SESSION, null);   // this extra is what makes it a Custom Tab
            intent.putExtras(extras);
            intent.putExtra(TOOLBAR_COLOR, toolbarColor);
            intent.putExtra(TITLE, 1);
            String browser = customTabsBrowser(activity);
            if (browser != null) intent.setPackage(browser);   // otherwise any browser opens it as a normal page
            activity.startActivity(intent);
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    // The default browser if it supports Custom Tabs, else the first one that does (Chrome on most phones).
    static String customTabsBrowser(Activity activity)
    {
        PackageManager pm = activity.getPackageManager();
        Intent web = new Intent(Intent.ACTION_VIEW, Uri.parse("https://accounts.google.com"));
        ResolveInfo preferred = pm.resolveActivity(web, 0);
        List<ResolveInfo> all = pm.queryIntentActivities(web, 0);
        if (preferred != null && preferred.activityInfo != null && supports(pm, preferred.activityInfo.packageName)) return preferred.activityInfo.packageName;
        for (ResolveInfo info : all)
            if (info.activityInfo != null && supports(pm, info.activityInfo.packageName)) return info.activityInfo.packageName;
        return null;
    }

    static boolean supports(PackageManager pm, String packageName)
    {
        Intent service = new Intent(SERVICE);
        service.setPackage(packageName);
        return pm.resolveService(service, 0) != null;
    }
}

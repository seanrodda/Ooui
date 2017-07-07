using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace Ooui
{
    static class Platform
    {
		static readonly Assembly iosAssembly;
		static readonly Type iosUIViewControllerType;
		static readonly Type iosUIApplicationType;
		static readonly Type iosUIWebViewType;
		static readonly Type iosNSUrl;
		static readonly Type iosNSUrlRequest;

		static Platform ()
		{
			var asms = AppDomain.CurrentDomain.GetAssemblies ().ToDictionary (
				x => x.GetName().Name);

			asms.TryGetValue ("Xamarin.iOS", out iosAssembly);
			if (iosAssembly != null) {
				iosUIViewControllerType = iosAssembly.GetType ("UIKit.UIViewController");
				iosUIApplicationType = iosAssembly.GetType ("UIKit.UIApplication");
				iosUIWebViewType = iosAssembly.GetType ("UIKit.UIWebView");
				iosNSUrl = iosAssembly.GetType ("Foundation.NSUrl");
				iosNSUrlRequest = iosAssembly.GetType ("Foundation.NSUrlRequest");
			}
		}

		public static void OpenBrowser (string url, object presenter)
		{
			if (iosAssembly != null) {
				OpenBrowserOniOS (url, presenter);
			}
			else {
				StartBrowserProcess (url);
			}
		}

		static void OpenBrowserOniOS (string url, object presenter)
		{
			var presenterType = GetObjectType (presenter);

			//
			// Find a presenter view controller
			// 1. Try the given presenter
			// 2. Find the key window vc
			// 3. Create a window?
			//
			object presenterViewController = null;
			if (presenter != null && iosUIViewControllerType.IsAssignableFrom (presenterType)) {
				presenterViewController = presenter;
			}

			if (presenterViewController == null) {
				var app = iosUIApplicationType.GetProperty ("SharedApplication").GetValue (null, null);
				var window = iosUIApplicationType.GetProperty ("KeyWindow").GetValue (app, null);
				if (window != null) {
					var rvc = window.GetType ().GetProperty ("RootViewController").GetValue (window, null);
					if (rvc != null) {
						var pvc = rvc.GetType ().GetProperty ("PresentedViewController").GetValue (rvc, null);
						presenterViewController = pvc ?? rvc;
					}
				}
			}

			if (presenterViewController == null) {
				throw new InvalidOperationException ("Cannot find a view controller from which to present");
			}

			//
			// Create the browser
			//
			var browserVC = Activator.CreateInstance (iosUIViewControllerType);
			var browserV = Activator.CreateInstance (iosUIWebViewType);

			var nsUrl = iosNSUrl.GetMethod ("FromString").Invoke (null, new object[] { url });
			var nsUrlRequest = iosNSUrlRequest.GetMethod ("FromUrl").Invoke (null, new object[] { nsUrl });
			iosUIWebViewType.GetMethod ("LoadRequest").Invoke (browserV, new object[] { nsUrlRequest });
			iosUIViewControllerType.GetProperty ("View").SetValue (browserVC, browserV, null);

			var m = iosUIViewControllerType.GetMethod ("PresentViewController");

			Console.WriteLine (presenterViewController);
			Console.WriteLine (browserVC);
			m.Invoke (presenterViewController, new object[] { browserVC, false, null });
		}

		static Type GetObjectType (object o)
		{
			var t = typeof (object);
			if (o is IReflectableType rt) {
				t = rt.GetTypeInfo ().AsType ();
			}
			else if (o != null) {
				t = o.GetType ();
			}
			return t;
		}

		static Process StartBrowserProcess (string url)
		{
			var cmd = url;
			var args = "";

			var osv = Environment.OSVersion;
			if (osv.Platform == PlatformID.Unix) {
				cmd = "open";
				args = url;
			}

			// var vs = Environment.GetEnvironmentVariables ();
			// foreach (System.Collections.DictionaryEntry kv in vs) {
			//     System.Console.WriteLine($"K={kv.Key}, V={kv.Value}");
			// }

			Console.WriteLine ($"Process.Start {cmd} {args}");
			return Process.Start (cmd, args);
		}
    }
}
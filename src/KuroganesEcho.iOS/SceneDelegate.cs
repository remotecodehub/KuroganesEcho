using System;
using Foundation;
using UIKit;

namespace KuroganesEcho.iOS
{
    [Register("SceneDelegate")]
    internal sealed class SceneDelegate : UIResponder, IUIWindowSceneDelegate
    {
        [Export("window")]
        public UIWindow Window { get; set; }

        [Export("scene:willConnectToSession:options:")]
        public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
        {
            if (scene is not UIWindowScene windowScene)
            {
                throw new InvalidOperationException("The game requires a UIWindowScene.");
            }

            var game = Program.RunGame();
            if (game.Services.GetService(typeof(UIWindow)) is not UIWindow gameWindow
                || game.Services.GetService(typeof(UIViewController)) is not UIViewController gameViewController)
            {
                throw new InvalidOperationException("MonoGame did not create its iOS window and view controller.");
            }

            Window = new UIWindow(windowScene)
            {
                RootViewController = gameViewController
            };

            gameWindow.Hidden = true;
            Window.MakeKeyAndVisible();
        }
    }
}

using KuroganesEcho.Core;
using Foundation;
using UIKit;

namespace KuroganesEcho.iOS
{
    [Register("AppDelegate")]
    internal class Program : UIApplicationDelegate
    {
        private static KuroganesEchoGame _game;

        /// <summary>
        /// Creates and starts the game for the connected iOS scene.
        /// </summary>
        internal static KuroganesEchoGame RunGame()
        {
            if (_game == null)
            {
                _game = new KuroganesEchoGame();
                _game.Run();
            }

            return _game;
        }

        /// <summary>
        /// The main entry point for the application. 
        /// This sets up the application and specifies the UIApplicationDelegate 
        /// class to handle application lifecycle events.
        /// </summary>
        /// <param name="args">Command-line arguments passed to the application.</param>
        static void Main(string[] args)
        {
            UIApplication.Main(args, null, typeof(Program));
        }
    }
}
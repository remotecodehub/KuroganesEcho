using System;
using System.Collections.Generic;
using System.Globalization;

namespace KuroganesEcho.Core.Localization;

internal static class LocalizedText
{
    private static readonly IReadOnlyDictionary<string, string> Portuguese = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Title"] = "Kuroganes Echo",
        ["Play"] = "Jogar",
        ["Options"] = "Opções",
        ["Quit"] = "Sair",
        ["Language"] = "Idioma",
        ["Back"] = "Voltar",
        ["Portuguese"] = "Português",
        ["English"] = "Inglês",
        ["French"] = "Francês",
        ["Act01"] = "Ato 1 - A Trilha das Lanternas Apagadas",
        ["TestMode"] = "MODO TESTE - MOVIMENTO",
        ["Actions"] = "AÇÕES",
        ["Move"] = "MOVA O PERSONAGEM",
        ["ExitHint"] = "ESC / VOLTAR"
    };

    private static readonly IReadOnlyDictionary<string, string> English = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Title"] = "Kurogane's Echo",
        ["Play"] = "Play",
        ["Options"] = "Options",
        ["Quit"] = "Quit",
        ["Language"] = "Language",
        ["Back"] = "Back",
        ["Portuguese"] = "Portuguese",
        ["English"] = "English",
        ["French"] = "French",
        ["Act01"] = "Act 1 - The Trail of Extinguished Lanterns",
        ["TestMode"] = "TEST MODE - MOVEMENT",
        ["Actions"] = "ACTIONS",
        ["Move"] = "MOVE THE PLAYER",
        ["ExitHint"] = "ESC / BACK"
    };

    private static readonly IReadOnlyDictionary<string, string> French = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Title"] = "L'Écho de Kurogane",
        ["Play"] = "Jouer",
        ["Options"] = "Options",
        ["Quit"] = "Quitter",
        ["Language"] = "Langue",
        ["Back"] = "Retour",
        ["Portuguese"] = "Portugais",
        ["English"] = "Anglais",
        ["French"] = "Français",
        ["Act01"] = "Acte 1 - Le Sentier des Lanternes Éteintes",
        ["TestMode"] = "MODE TEST - MOUVEMENT",
        ["Actions"] = "ACTIONS",
        ["Move"] = "DÉPLACEZ LE PERSONNAGE",
        ["ExitHint"] = "ESC / RETOUR"
    };

    public static string Get(string key)
    {
        var table = CultureInfo.CurrentUICulture.Name switch
        {
            "fr-FR" => French,
            "en-US" => English,
            _ => Portuguese
        };

        return table.TryGetValue(key, out var value) ? value : key;
    }
}

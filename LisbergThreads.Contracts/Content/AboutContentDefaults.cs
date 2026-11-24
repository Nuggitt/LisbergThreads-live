using System.Collections.ObjectModel;

namespace LisbergThreads.Contracts.Content;

public static class AboutContentDefaults
{
    public static IReadOnlyList<string> SewingSteps { get; } = Array.AsReadOnly(new[]
    {
        "Stof, mønster og mål", "Udklipning og syning", "Det færdige produkt"
    });

    public static IReadOnlyList<string> KnittingSteps { get; } = Array.AsReadOnly(new[]
    {
        "Garn, opskrift og mål", "Maske for maske", "Det færdige strik"
    });

    public static IReadOnlyDictionary<string, string> Values { get; } = CreateValues();

    public static bool IsImageKey(string key) => Values.ContainsKey(key) && key.EndsWith(".Image", StringComparison.Ordinal);

    private static IReadOnlyDictionary<string, string> CreateValues()
    {
        var values = new Dictionary<string, string>
        {
            ["About.Title"] = "Om Lisberg Threads",
            ["About.Eyebrow"] = "Mennesket og håndarbejdet bag",
            ["About.Nav.History"] = "Historien bag",
            ["About.Nav.Sewing"] = "Fra stof til produkt",
            ["About.Nav.Knitting"] = "Fra garn til strik",
            ["About.History.Eyebrow"] = "Historien bag",
            ["About.History.Title"] = "Fra de første projekter til Lisberg Threads",
            ["About.History.Start.Title"] = "De første sting",
            ["About.History.Machines.Title"] = "Symaskinen – dengang og nu",
            ["About.History.Knitting.Title"] = "Glæden ved strik",
            ["About.History.First.Caption"] = "De første projekter og symaskinen",
            ["About.History.Today.Caption"] = "Håndarbejdet og udstyret i dag",
            ["About.Sewing.Title"] = "Fra stof til færdigt produkt",
            ["About.Sewing.Subtitle"] = "Et indblik i syprocessen",
            ["About.Knitting.Title"] = "Fra garn til færdigt strik",
            ["About.Knitting.Subtitle"] = "Et indblik i strikkeprocessen",
            ["About.Visit.Title"] = "Se det færdige håndarbejde",
            ["About.Visit.Text"] = "Gå på opdagelse blandt de syede og strikkede produkter i butikken.",
            ["About.Visit.Link"] = "Se produkter",
            ["About.Introduction"] = "Kom med bag håndarbejdet hos Lisberg Threads – fra de første materialer til syede og strikkede produkter.",
            ["About.History.Introduction"] = "Her kommer den personlige historie bag Lisberg Threads og glæden ved at skabe med hænderne.",
            ["About.History.Start"] = "Historien om de første syprojekter kommer snart.",
            ["About.History.Machines"] = "Her kommer fortællingen om den første symaskine og udstyret i dag.",
            ["About.History.Knitting"] = "Historien om vejen ind i strikningen kommer snart.",
            ["About.History.First.Image"] = "[]",
            ["About.History.Today.Image"] = "[]"
        };
        var sewing = new[]
        {
            "Stof og materialer vælges og bestilles. Opskrift, symønster og mål danner udgangspunkt for produktets form og størrelse.",
            "Mønsterdelene klippes ud af stoffet og samles ved symaskinen. Produktet tager form søm for søm.",
            "De sidste detaljer færdiggøres, og resultatet er klar til at blive vist frem."
        };
        var knitting = new[]
        {
            "Garn, farver og opskrift vælges. Strikkeprøve og mål hjælper med at tilpasse størrelse og strikkefasthed.",
            "Arbejdet vokser frem maske for maske, mens mønster og mål følges undervejs.",
            "Delene samles, og enderne hæftes. Det færdige strik viser resultatet af arbejdet fra garn til produkt."
        };
        for (var i = 0; i < SewingSteps.Count; i++)
        {
            values[$"About.Sewing.{i}.Title"] = SewingSteps[i];
            values[$"About.Sewing.{i}.Caption"] = SewingSteps[i];
            values[$"About.Sewing.{i}.Text"] = sewing[i];
            values[$"About.Sewing.{i}.Image"] = "[]";
            values[$"About.Knitting.{i}.Text"] = knitting[i];
            values[$"About.Knitting.{i}.Title"] = KnittingSteps[i];
            values[$"About.Knitting.{i}.Caption"] = KnittingSteps[i];
            values[$"About.Knitting.{i}.Image"] = "[]";
        }
        return new ReadOnlyDictionary<string, string>(values);
    }
}

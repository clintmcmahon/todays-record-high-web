namespace TodaysRecordHigh.Web.Tests.Integration.TestSupport;

/// <summary>
/// Loads a captured real ACIS response from Integration/Fixtures. Each file is the
/// verbatim body of a live https://data.rcc-acis.org/ request made while building this
/// test suite (see the curl command in the comment at the top of the test that uses
/// it), not hand-written JSON — so parsing bugs against real response quirks (trace
/// values, the [value, mcnt] cell shape, etc.) show up here.
/// </summary>
public static class Fixture
{
    public static string Load(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Fixture not found: {path}. " +
                "Fixtures are copied to the output directory — rebuild if this file was just added.");
        return File.ReadAllText(path);
    }
}

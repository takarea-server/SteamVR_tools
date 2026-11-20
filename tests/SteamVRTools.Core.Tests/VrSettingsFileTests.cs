using System.Text.Json.Nodes;
using SteamVRTools.Core;

namespace SteamVRTools.Core.Tests;

public class VrSettingsFileTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "svrt-" + Guid.NewGuid().ToString("N"));

    public VrSettingsFileTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private string Write(string content)
    {
        var path = Path.Combine(_dir, "steamvr.vrsettings");
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public void ReadMode_NoDriverPicoSection_ReturnsPico()
    {
        var f = new VrSettingsFile(Write("{\"steamvr\":{\"a\":1}}"));
        Assert.Equal(Mode.Pico, f.ReadMode());
    }

    [Fact]
    public void ReadMode_EnableFalse_ReturnsBeyond()
    {
        var f = new VrSettingsFile(Write("{\"driver_pico\":{\"enable\":false}}"));
        Assert.Equal(Mode.Beyond, f.ReadMode());
    }

    [Fact]
    public void ReadMode_MissingFile_Throws()
    {
        var f = new VrSettingsFile(Path.Combine(_dir, "nope.vrsettings"));
        Assert.Throws<VrSettingsException>(() => f.ReadMode());
    }

    [Fact]
    public void ReadMode_InvalidJson_Throws()
    {
        var f = new VrSettingsFile(Write("{ not json"));
        Assert.Throws<VrSettingsException>(() => f.ReadMode());
    }

    [Fact]
    public void ReadMode_EmptyFile_Throws()
    {
        var f = new VrSettingsFile(Write(""));
        Assert.Throws<VrSettingsException>(() => f.ReadMode());
    }

    [Fact]
    public void WriteMode_Beyond_AddsEnableFalse_KeepsOtherSections()
    {
        var path = Write("{\"steamvr\":{\"a\":1}}");
        new VrSettingsFile(path).WriteMode(Mode.Beyond);
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Equal(1, (int)root["steamvr"]!["a"]!);
        Assert.False((bool)root["driver_pico"]!["enable"]!);
    }

    [Fact]
    public void WriteMode_Pico_RemovesEnable_RemovesEmptySection()
    {
        var path = Write("{\"steamvr\":{\"a\":1},\"driver_pico\":{\"enable\":false}}");
        new VrSettingsFile(path).WriteMode(Mode.Pico);
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Null(root["driver_pico"]);
        Assert.Equal(1, (int)root["steamvr"]!["a"]!);
    }

    [Fact]
    public void WriteMode_Pico_KeepsOtherKeysInSection()
    {
        var path = Write("{\"driver_pico\":{\"enable\":false,\"x\":5}}");
        new VrSettingsFile(path).WriteMode(Mode.Pico);
        var root = JsonNode.Parse(File.ReadAllText(path))!;
        Assert.Null(root["driver_pico"]!["enable"]);
        Assert.Equal(5, (int)root["driver_pico"]!["x"]!);
    }

    [Fact]
    public void WriteMode_InvalidJson_Throws_FileUnchanged()
    {
        var path = Write("{ not json");
        Assert.Throws<VrSettingsException>(() => new VrSettingsFile(path).WriteMode(Mode.Beyond));
        Assert.Equal("{ not json", File.ReadAllText(path));
    }
}

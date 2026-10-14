using System.Text.Json.Nodes;
using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.Meta;

public class SettingsDataTest : TestCase
{
	public void TestDefaultVolumeIsFull() => Assert.AlmostEq(new SettingsData().MasterVolume, 1.0);

	public void TestVolumeIsClampedToZeroOne()
	{
		var settings = new SettingsData { MasterVolume = 3f };
		Assert.AlmostEq(settings.MasterVolume, 1.0);
		settings.MasterVolume = -2f;
		Assert.AlmostEq(settings.MasterVolume, 0.0);
	}

	public void TestJsonRoundTrip()
	{
		var settings = new SettingsData { MasterVolume = 0.25f };
		SettingsData? restored = SettingsData.FromJson(settings.ToJson());
		Assert.NotNull(restored);
		Assert.AlmostEq(restored!.MasterVolume, 0.25);
	}

	public void TestMissingKeyKeepsTheDefault() => Assert.AlmostEq(SettingsData.FromJson(new JsonObject())!.MasterVolume, 1.0);

	public void TestWrongTypeIsRejected() => Assert.Null(SettingsData.FromJson(SaveJson.Object("""{"master_volume": "loud"}""")));

	public void TestOutOfRangeVolumeFromAFileIsClampedNotRejected()
	{
		Assert.AlmostEq(SettingsData.FromJson(SaveJson.Object("""{"master_volume": 7}"""))!.MasterVolume, 1.0);
	}
}

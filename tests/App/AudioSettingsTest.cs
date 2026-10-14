using Godot;
using VampireSurvivor.App;
using VampireSurvivor.Meta;
using VampireSurvivor.Tests.Support;

namespace VampireSurvivor.Tests.App;

public class AudioSettingsTest : TestCase
{
	private float _savedDb;

	private static int Master => AudioServer.GetBusIndex("Master");

	private static float MasterDb => AudioServer.GetBusVolumeDb(Master);

	public override void BeforeEach() => _savedDb = MasterDb;

	public override void AfterEach() => AudioServer.SetBusVolumeDb(Master, _savedDb);

	public void TestHalfVolumeSetsTheMatchingDecibels()
	{
		AudioSettings.Apply(new SettingsData { MasterVolume = 0.5f });
		Assert.AlmostEq(MasterDb, Mathf.LinearToDb(0.5f), 0.01);
	}

	public void TestFullVolumeIsZeroDb()
	{
		AudioSettings.Apply(new SettingsData());
		Assert.AlmostEq(MasterDb, 0.0, 0.01);
	}

	public void TestZeroVolumeIsFiniteAndEffectivelySilent()
	{
		AudioSettings.Apply(new SettingsData { MasterVolume = 0f });
		Assert.True(float.IsFinite(MasterDb), "no -inf");
		Assert.True(MasterDb <= -60f);
	}
}

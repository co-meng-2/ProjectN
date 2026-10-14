using System.Collections.Generic;

namespace VampireSurvivor.Defs;

/// <summary>
/// Something that can list what is wrong with it (empty = valid). Scene roots referenced from defs (event controllers)
/// implement it so a def can validate the scene it points at without naming the higher-layer class.
/// </summary>
public interface IValidatable
{
	List<string> Validate();
}

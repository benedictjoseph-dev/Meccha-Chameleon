using UnityEngine;

namespace Core
{
    /// <summary>
    /// Abstraction for player input. 
    /// This allows easy swapping of input systems (e.g., Old vs New Unity Input System, AI controllers, etc.).
    /// </summary>
    public interface IPlayerInput
    {
        Vector2 Move { get; }
        bool Jump { get; }
        bool Sprint { get; }
    }
}

using QFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

[DisallowMultipleComponent]
[AddComponentMenu("Game/Data Binding")]
public sealed class DataBindingComponent : GameFrameworkComponent
{
    public BindableProperty<T> CreateProperty<T>(T defaultValue = default)
    {
        return new BindableProperty<T>(defaultValue);
    }
}

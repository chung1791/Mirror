namespace Mirror.Examples.Tanks
{
    public class Test : NetworkBehaviour
    {
        [SyncVar(hook=nameof(OnChanged))] public int health = 42;

        void OnChanged(int oldValue, int newValue) {}
    }
}

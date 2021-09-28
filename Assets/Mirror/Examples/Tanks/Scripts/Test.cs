namespace Mirror.Examples.Tanks
{
    public class Test : NetworkBehaviour
    {
        [SyncVar] public int health = 42;
        public SyncVar<int> yo = new SyncVar<int>(42);
    }
}

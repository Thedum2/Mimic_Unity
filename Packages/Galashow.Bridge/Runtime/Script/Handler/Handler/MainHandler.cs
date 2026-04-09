namespace Mimic.Bridge
{
    public class MainHandler
    {
        public MatchHandler MatchHandler { get; private set; }
        public MathHandler MathHandler { get; private set; }
        public LobbyChatHandler LobbyChatHandler { get; private set; }

        public void Initialize()
        {
            MatchHandler = new MatchHandler();
            MathHandler = new MathHandler();
            LobbyChatHandler = new LobbyChatHandler();
        }
    }
}

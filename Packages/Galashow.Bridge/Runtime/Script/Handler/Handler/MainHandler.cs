namespace Mimic.Bridge
{
    public class MainHandler
    {
        public MatchHandler MatchHandler { get; private set; }
        public RoundHandler RoundHandler { get; private set; }
        public ConversationHandler ConversationHandler { get; private set; }
        public LobbyChatHandler LobbyChatHandler { get; private set; }
        public PlayerHandler PlayerHandler { get; private set; }

        public void Initialize()
        {
            MatchHandler = new MatchHandler();
            RoundHandler = new RoundHandler();
            ConversationHandler = new ConversationHandler();
            LobbyChatHandler = new LobbyChatHandler();
            PlayerHandler = new PlayerHandler();
        }
    }
}

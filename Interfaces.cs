namespace Devmachinist.Constellations
{
    /// <summary>
    /// Handles all outgoing messages including those routed through the server.
    /// This can create conflict with the IServerMessageHandler because both will be called when the server is used as a router.
    /// </summary>
    public interface IMessageSenderHandler
    {
        public Constellation.Message Handle(Constellation.Message message);
    }
    /// <summary>
    /// Handles incoming messages sent to this constellation
    /// </summary>
    public interface IMessageHandler
    {
        public Constellation.Message Handle(Constellation.Message message);
    }
    /// <summary>
    /// Handles failed sent messages
    /// </summary>
    public interface IFailedMessageHandler
    {
        public Constellation.Message Handle(string reason, Constellation.Message message);
    }
    /// <summary>
    /// Handles incoming messages sent to other constellations through this server
    /// </summary>
    public interface IServerMessageHandler
    {
        public Constellation.Message Handle(Constellation.Message message);
    }
    /// <summary>
    /// Handles incoming or outgoing clients
    /// </summary>
    public interface IClientHandler
    {
        public NamedClient Handle(NamedClient namedClient);
    }
    /// <summary>
    /// Handles incoming streams before they are serialized into messages.Will be encrypted if sent by another constellation.
    /// </summary>
    public interface IStreamReceiver
    {
        public byte[] Receive(byte[] bytes);
    }
    /// <summary>
    /// Handles outgoing streams after they are serialized into streams. Will be encrypted by this constellation.
    /// </summary>
    public interface IStreamSender
    {
        public byte[] Send(byte[] bytes);
    }
}

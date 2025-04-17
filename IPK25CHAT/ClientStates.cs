
public class ClientStates
{
    public enum States
    {
        START,
        AUTH,
        OPEN,
        JOIN,
        END
    }

    public enum MessageTypes
    {
        CONFIRM = 0x00,
        REPLY = 0x01,
        AUTH = 0x02,
        JOIN = 0x03,
        MSG = 0x04,
        PING = 0xFD,
        ERR = 0xFE,
        BYE = 0xFF
    }
}
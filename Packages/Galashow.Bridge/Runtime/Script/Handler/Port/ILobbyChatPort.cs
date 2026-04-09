using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public interface ILobbyChatPort
    {
        void R2U_LobbyChatManager_SubmitMessage_REQ(
            Request.R2U.LobbyChatManagerSubmitMessage data,
            Action<Acknowledge.U2R.LobbyChatManagerSubmitMessage> onSuccess,
            Action<string> onError);
    }
}

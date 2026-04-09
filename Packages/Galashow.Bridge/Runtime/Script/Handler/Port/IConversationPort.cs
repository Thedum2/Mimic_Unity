using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public interface IConversationPort
    {
        void R2U_ConversationManager_SubmitMessage_REQ(
            Request.R2U.ConversationManagerSubmitMessage data,
            Action<Acknowledge.U2R.ConversationManagerMessageAccepted> onSuccess,
            Action<string> onError);
    }
}

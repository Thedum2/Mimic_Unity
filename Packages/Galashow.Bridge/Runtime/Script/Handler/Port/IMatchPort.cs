using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public interface IMatchPort
    {
        void R2U_MatchManager_CreateRoom_REQ(
            Request.R2U.MatchManagerCreateRoom data,
            Action<Acknowledge.U2R.MatchManagerCreateRoom> onSuccess,
            Action<string> onError);

        void R2U_MatchManager_JoinRoomByInviteCode_REQ(
            Request.R2U.MatchManagerJoinRoomByInviteCode data,
            Action<Acknowledge.U2R.MatchManagerJoinRoomByInviteCode> onSuccess,
            Action<string> onError);

        void R2U_MatchManager_RejoinRoom_REQ(
            Request.R2U.MatchManagerRejoinRoom data,
            Action<Acknowledge.U2R.MatchManagerRejoinRoom> onSuccess,
            Action<string> onError);
    }
}

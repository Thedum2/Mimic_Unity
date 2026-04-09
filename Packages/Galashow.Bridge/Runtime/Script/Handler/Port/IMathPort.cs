using System;
using Mimic.Bridge.Model;

namespace Mimic.Bridge
{
    public interface IMathPort
    {
        void R2U_MathManager_Add_REQ(
            Request.R2U.MathAdd data,
            Action<Acknowledge.U2R.MathAdd> onSuccess,
            Action<string> onError);
    }
}

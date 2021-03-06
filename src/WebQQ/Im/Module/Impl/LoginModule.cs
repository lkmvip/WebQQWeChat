using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using FclEx.Extensions;
using HttpAction;
using HttpAction.Event;
using Microsoft.Extensions.Logging;
using WebQQ.Im.Action;
using WebQQ.Im.Core;
using WebQQ.Im.Event;
using WebQQ.Im.Module.Interface;

namespace WebQQ.Im.Module.Impl
{
    /// <summary>
    /// <para>��¼ģ�飬������¼���˳�</para>
    /// </summary>
    public class LoginModule : QQModule, ILoginModule
    {
        public void BeginPoll()
        {
            new PollMsgAction(Context, async (sender, @event) => // 1.��ȡ��ά��
            {
                if (@event.Type == ActionEventType.EvtOK)
                {
                    foreach (var e in (List<QQNotifyEvent>)@event.Target)
                    {
                        Context.FireNotifyAsync(e).Forget();
                    }
                    await Task.CompletedTask;
                }
            }).ExecuteForeverAsync().Forget();
        }

        public LoginModule(IQQContext context) : base(context)
        {
        }

        public Task<ActionEvent> Login(ActionEventListener listener)
        {
            return new WebQQActionFuture(Context, listener)
                .PushAction<GetQRCodeAction>(async (sender, @event) => // 1.��ȡ��ά��
                {
                    if (@event.Type == ActionEventType.EvtOK)
                    {
                        var verify = (Image)@event.Target;
                        await Context.FireNotifyAsync(QQNotifyEvent.CreateEvent(QQNotifyEventType.QRCodeReady, verify));
                    }
                })
                .PushAction<CheckQRCodeAction>(async (sender, @event) => // 2.��ȡ��ά��ɨ��״̬
                {
                    if (@event.Type != ActionEventType.EvtOK) return;

                    var args = (CheckQRCodeArgs)@event.Target;
                    switch (args.Status)
                    {
                        case QRCodeStatus.OK:
                            Session.CheckSigUrl = args.Msg;
                            await Context.FireNotifyAsync(QQNotifyEvent.CreateEvent(QQNotifyEventType.QRCodeSuccess));
                            break;

                        case QRCodeStatus.Valid:
                        case QRCodeStatus.Auth:
                            Logger.LogDebug($"��ά��״̬��{args.Status.GetDescription()}");
                            @event.Type = ActionEventType.EvtRepeat;
                            await Task.Delay(3000);
                            break;

                        case QRCodeStatus.Invalid:
                            await Context.FireNotifyAsync(QQNotifyEvent.CreateEvent(QQNotifyEventType.QRCodeInvalid, args.Msg));
                            break;
                    }
                })
                .PushAction<CheckSigAction>()
                .PushAction<GetVfwebqqAction>()
                .PushAction<ChannelLoginAction>(async (sender, @event) =>
                {
                    if (@event.Type != ActionEventType.EvtOK) return;
                    await Context.FireNotifyAsync(QQNotifyEvent.CreateEvent(QQNotifyEventType.LoginSuccess));
                })
                .PushAction<GetFriendsAction>(async (sender, @event) =>
                {
                    if (@event.Type != ActionEventType.EvtOK) return;
                    var obj = Store.FriendDic.FirstOrDefault().Value;
                    if (obj == null) return;
                    await new GetFriendLongNickAction(Context, obj).ExecuteAsyncAuto();
                    await new GetFriendQQNumberAction(Context, obj).ExecuteAsyncAuto();
                    await new GetFriendInfoAction(Context, obj).ExecuteAsyncAuto();
                })
                .PushAction<GetGroupNameListAction>(async (sender, @event) =>
                {
                    if (@event.Type != ActionEventType.EvtOK) return;
                    var group = Store.GroupDic.FirstOrDefault().Value;
                    if (group != null)
                    {
                        await new GetGroupInfoAction(Context, group).ExecuteAsyncAuto();
                    }
                })
                .PushAction<GetDiscussionListAction>(async (sender, @event) =>
                {
                    if (@event.Type != ActionEventType.EvtOK) return;
                    var dis = Store.DiscussionDic.FirstOrDefault().Value;
                    if (dis != null)
                    {
                        await new GetDiscussionInfoAction(Context, dis).ExecuteAsyncAuto();
                    }
                })
                .PushAction<GetSelfInfoAction>()
                .PushAction<GetOnlineFriendsAction>()
                .ExecuteAsync();
        }
    }

}

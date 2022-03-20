﻿using System.Threading.Tasks;
using Konata.Core.Attributes;
using Konata.Core.Events;
using Konata.Core.Events.Model;

// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable InconsistentNaming

namespace Konata.Core.Components.Logics.Model;

[EventSubscribe(typeof(PushConfigEvent))]
[EventSubscribe(typeof(PushNotifyEvent))]
[EventSubscribe(typeof(OnlineReqPushEvent))]
[EventSubscribe(typeof(PushTransMsgEvent))]
[BusinessLogic("PushEvent Logic", "Forward push events to userend.")]
internal class PushEventLogic : BaseLogic
{
    private const string TAG = "PushEvent Logic";

    internal PushEventLogic(BusinessComponent context)
        : base(context)
    {
    }

    public override async Task Incoming(ProtocolEvent e)
    {
        switch (e)
        {
            // Handle push config
            case PushConfigEvent push:
                OnPushConfig(push);
                break;

            // Handle online push
            case OnlineReqPushEvent reqpush:
                await OnOnlineReqPush(reqpush);
                break;

            // Handle online push trans
            case PushTransMsgEvent transpush:
                OnPushTransMsg(transpush);
                break;

            // Handle push notify event
            case PushNotifyEvent notifypush:
                await OnPushNotify(notifypush);
                break;

            // Just forward messages to userend
            default:
                Context.PostEventToEntity(e);
                break;
        }
    }

    /// <summary>
    /// Push config
    /// </summary>
    /// <param name="e"></param>
    private void OnPushConfig(PushConfigEvent e)
    {
        // Update the config
        ConfigComponent.HighwayConfig = new()
        {
            Server = e.HighwayHost,
            Ticket = e.HighwayTicket
        };
        Context.LogI(TAG, $"Highway server has changed {e.HighwayHost}");
    }

    /// <summary>
    /// Online push
    /// </summary>
    /// <param name="e"></param>
    private async Task OnOnlineReqPush(OnlineReqPushEvent e)
    {
        // Post inner event
        if (e.InnerEvent != null)
            Context.PostEventToEntity(e.InnerEvent);

        // Confirm push
        await ConfrimReqPushEvent(Context, e);
    }

    /// <summary>
    /// Trans msg push
    /// </summary>
    /// <param name="e"></param>
    private async void OnPushTransMsg(PushTransMsgEvent e)
    {
        // Post inner event
        if (e.InnerEvent != null)
            Context.PostEventToEntity(e.InnerEvent);

        // Confirm push
        await ConfrimPushTransMsgEvent(Context, e);
    }

    private async Task OnPushNotify(PushNotifyEvent e)
    {
        switch (e.Type)
        {
            case NotifyType.NewMember:
            case NotifyType.GroupCreated:
            case NotifyType.GroupRequestAccepted:
            case NotifyType.FriendMessage:
            case NotifyType.FriendMessageSingle:
            case NotifyType.FriendPttMessage:
            case NotifyType.StrangerMessage:
            case NotifyType.FriendFileMessage:
                await OnPullNewMessage();
                break;

            case NotifyType.GroupRequest:
            case NotifyType.GroupRequest525:
            case NotifyType.GroupInvitation:
                // ProfileService.Pb.ReqSystemMsgNew.Group
                break;

            case NotifyType.FriendRequest:
            case NotifyType.FriendIncreaseSingle:
                // ProfileService.Pb.ReqSystemMsgNew.Friend
                break;

            default:
            case NotifyType.BlackListUpdate:
                break;
        }
    }

    private async Task OnPullNewMessage()
    {
        var result = await PullMessage(Context, ConfigComponent.SyncCookie);

        // Update sync cookie
        if (result.SyncCookie != null)
            ConfigComponent.SyncCookie = result.SyncCookie;
    }

    #region Stub methods

    private static Task<OnlineRespPushEvent> ConfrimReqPushEvent(BusinessComponent context, OnlineReqPushEvent original)
        => context.SendPacket<OnlineRespPushEvent>(OnlineRespPushEvent.Create(context.Bot.Uin, original));

    private static Task<OnlineRespPushEvent> ConfrimPushTransMsgEvent(BusinessComponent context, PushTransMsgEvent original)
        => context.SendPacket<OnlineRespPushEvent>(OnlineRespPushEvent.Create(context.Bot.Uin, original));

    private static Task<PbGetMessageEvent> PullMessage(BusinessComponent context, byte[] syncCookie)
        => context.SendPacket<PbGetMessageEvent>(PbGetMessageEvent.Create(syncCookie));

    #endregion
}

using System;
using MediatR;
using OpcRouter.Models.Entities.DeviceEntity;

namespace OpcRouter.Messages.Commands;

public class PublishToKafkaCommand : IRequest
{
    public string Topic { get; set; }
    public Device Device { get; set; }
    public TimeSpan Timeout { get; set; }
}
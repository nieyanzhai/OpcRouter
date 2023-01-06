using MediatR;
using Secs4Net;

namespace OpcRouter.Messages.Commands;

public class SecsReceivePrimaryMessageCommand : IRequest
{
    public SecsMessage? Message { get; set; }
}
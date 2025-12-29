using Application.Abstractions.Messaging;

namespace Application.Features.ImageKit.Commands;

public record DeleteImageKitFileCommand(string FileId) : ICommand;

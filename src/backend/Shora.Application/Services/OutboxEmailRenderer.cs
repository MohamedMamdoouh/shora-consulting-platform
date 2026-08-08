using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Email;
using Shora.Application.Email.Outbox;
using Shora.Application.Options;
using Shora.Domain.Entities;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class OutboxEmailRenderer(
    IApplicationDbContext dbContext,
    IEmailTemplateService emailTemplateService,
    TransactionEmailLinks links,
    IOptions<EmailBrandOptions> brandOptions) : IOutboxEmailRenderer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly EmailBrandOptions _brand = brandOptions.Value;

    public async Task<Result<OutboxEmailRenderResult>> RenderAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        var contextResult = await BuildContextAsync(message, cancellationToken);
        if (contextResult.IsFailure)
        {
            return contextResult.Error!;
        }

        var context = contextResult.Value!;
        var templateRequest = TransactionEmailTemplates.BuildRequest(context, links, _brand.BrandName);
        var subject = TransactionEmailTemplates.GetSubject(message.MessageType, _brand.BrandName);
        var htmlBody = emailTemplateService.Render(templateRequest);

        return new OutboxEmailRenderResult(context.Recipient.Email!, subject, htmlBody);
    }

    private async Task<Result<TransactionEmailContext>> BuildContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        return message.MessageType switch
        {
            OutboxMessageTypes.ClientBookingConfirmedEmail =>
                await BuildBookingPaymentReceiptContextAsync(message, cancellationToken, RecipientKind.Client),

            OutboxMessageTypes.AdminNewBookingEmail or
            OutboxMessageTypes.AdminReceiptUploadedEmail =>
                await BuildBookingPaymentReceiptContextAsync(message, cancellationToken, RecipientKind.Admin),

            OutboxMessageTypes.ClientReceiptDeclinedEmail =>
                await BuildClientReceiptDeclinedContextAsync(message, cancellationToken),

            OutboxMessageTypes.ClientBookingCancelledEmail =>
                await BuildBookingClientContextAsync(message, cancellationToken),

            OutboxMessageTypes.AdminNewCancellationRequestEmail =>
                await BuildAdminNewCancellationRequestContextAsync(message, cancellationToken),

            OutboxMessageTypes.ClientCancellationRequestDeclinedEmail =>
                await BuildClientCancellationRequestDeclinedContextAsync(message, cancellationToken),

            OutboxMessageTypes.ClientRefundConfirmationEmail =>
                await BuildClientRefundConfirmationContextAsync(message, cancellationToken),

            OutboxMessageTypes.AdminRefundRevocationEmail =>
                await BuildAdminRefundRevocationContextAsync(message, cancellationToken),

            _ => Error.Validation(
                ErrorCodes.General.Unexpected,
                $"Unsupported outbox message type '{message.MessageType}'.")
        };
    }

    private enum RecipientKind
    {
        Client,
        Admin
    }

    private async Task<Result<TransactionEmailContext>> BuildBookingPaymentReceiptContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken,
        RecipientKind recipientKind)
    {
        var payload = Deserialize<BookingPaymentReceiptPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await LoadBookingAsync(payload.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = recipientKind == RecipientKind.Admin
            ? await LoadAdminRecipientAsync(cancellationToken)
            : await LoadClientRecipientAsync(payload.ClientId, cancellationToken);

        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = booking.Payment
        };
    }

    private async Task<Result<TransactionEmailContext>> BuildClientReceiptDeclinedContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<ClientReceiptDeclinedPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await LoadBookingAsync(payload.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = await LoadClientRecipientAsync(payload.ClientId, cancellationToken);
        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = booking.Payment,
            ReasonCode = payload.ReasonCode,
            ReasonNote = payload.ReasonNote,
            ReceiptUploadDeadlineUtc = payload.ReceiptUploadDeadlineUtc
        };
    }

    private async Task<Result<TransactionEmailContext>> BuildBookingClientContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<BookingClientPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await LoadBookingAsync(payload.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = await LoadClientRecipientAsync(payload.ClientId, cancellationToken);
        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = booking.Payment
        };
    }

    private async Task<Result<TransactionEmailContext>> BuildAdminNewCancellationRequestContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<BookingClientPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Client)
            .Include(b => b.CancellationRequest)
            .FirstOrDefaultAsync(b => b.Id == payload.BookingId, cancellationToken);

        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = await LoadAdminRecipientAsync(cancellationToken);
        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = booking.Payment,
            ClientReason = booking.CancellationRequest?.ClientReason,
            AutoDeclineAtUtc = booking.CancellationRequest?.AutoDeclineAtUtc
        };
    }

    private async Task<Result<TransactionEmailContext>> BuildClientCancellationRequestDeclinedContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<ClientCancellationRequestDeclinedPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await LoadBookingAsync(payload.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = await LoadClientRecipientAsync(payload.ClientId, cancellationToken);
        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = booking.Payment,
            ReasonCode = payload.ReasonCode,
            ReasonNote = payload.ReasonNote
        };
    }

    private async Task<Result<TransactionEmailContext>> BuildClientRefundConfirmationContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<ClientRefundConfirmationPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await LoadBookingAsync(payload.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = await LoadClientRecipientAsync(payload.ClientId, cancellationToken);
        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        var payment = booking.Payment
            ?? await dbContext.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payload.PaymentId, cancellationToken);

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = payment,
            RefundReference = payload.Reference,
            RefundNote = payload.Note,
            RefundAmount = payload.Amount,
            RefundCurrency = payload.Currency
        };
    }

    private async Task<Result<TransactionEmailContext>> BuildAdminRefundRevocationContextAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var payload = Deserialize<AdminRefundRevocationPayload>(message.PayloadJson);
        if (payload is null)
        {
            return InvalidPayload(message.MessageType);
        }

        var booking = await LoadBookingAsync(payload.BookingId, cancellationToken);
        if (booking is null)
        {
            return BookingNotFound(payload.BookingId);
        }

        var settings = await LoadSettingsAsync(cancellationToken);
        if (settings is null)
        {
            return SettingsNotFound();
        }

        var recipientResult = await LoadAdminRecipientAsync(cancellationToken);
        if (recipientResult.IsFailure)
        {
            return recipientResult.Error!;
        }

        var payment = booking.Payment
            ?? await dbContext.Payments.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == payload.PaymentId, cancellationToken);

        return new TransactionEmailContext
        {
            MessageType = message.MessageType,
            Recipient = recipientResult.Value!,
            Booking = booking,
            Settings = settings,
            Payment = payment,
            PreviousRefundReference = payload.PreviousReference,
            CorrectionReason = payload.CorrectionReason
        };
    }

    private async Task<Booking?> LoadBookingAsync(Guid bookingId, CancellationToken cancellationToken) =>
        await dbContext.Bookings
            .AsNoTracking()
            .Include(b => b.Client)
            .Include(b => b.Payment)
            .FirstOrDefaultAsync(b => b.Id == bookingId, cancellationToken);

    private async Task<Settings?> LoadSettingsAsync(CancellationToken cancellationToken) =>
        await dbContext.Settings
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.Id == Settings.SingletonId, cancellationToken);

    private async Task<Result<ApplicationUser>> LoadClientRecipientAsync(
        Guid clientId,
        CancellationToken cancellationToken)
    {
        var client = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == clientId, cancellationToken);

        if (client is null)
        {
            return Error.NotFound(ErrorCodes.Auth.UserNotFound, "Client was not found.");
        }

        if (string.IsNullOrWhiteSpace(client.Email))
        {
            return Error.Validation(ErrorCodes.General.Unexpected, "Client email is missing.");
        }

        return client;
    }

    private async Task<Result<ApplicationUser>> LoadAdminRecipientAsync(CancellationToken cancellationToken)
    {
        var admin = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Admin)
            .OrderBy(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken);

        if (admin is null)
        {
            return Error.NotFound(ErrorCodes.Auth.UserNotFound, "Admin user was not found.");
        }

        if (string.IsNullOrWhiteSpace(admin.Email))
        {
            return Error.Validation(ErrorCodes.General.Unexpected, "Admin email is missing.");
        }

        return admin;
    }

    private static T? Deserialize<T>(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(payloadJson, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static Error InvalidPayload(string messageType) =>
        Error.Validation(
            ErrorCodes.General.Unexpected,
            $"Outbox payload for '{messageType}' is invalid.");

    private static Error BookingNotFound(Guid bookingId) =>
        Error.NotFound(ErrorCodes.Booking.NotFound, $"Booking '{bookingId}' was not found.");

    private static Error SettingsNotFound() =>
        Error.NotFound(ErrorCodes.Settings.NotFound, "Settings are not configured.");
}

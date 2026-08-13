import { describe, expect, it } from 'vitest';
import { formatReceiptDeclineReasonCode } from './receipt-decline-labels.util';

describe('formatReceiptDeclineReasonCode', () => {
  it('translates known receipt decline reason codes to Arabic', () => {
    expect(formatReceiptDeclineReasonCode('AmountMismatch')).toBe('المبلغ غير مطابق');
    expect(formatReceiptDeclineReasonCode('UnreadableImage')).toBe('صورة غير واضحة');
  });

  it('returns free-text notes unchanged', () => {
    expect(formatReceiptDeclineReasonCode('Sent 400 EGP')).toBe('Sent 400 EGP');
  });

  it('returns null for empty values', () => {
    expect(formatReceiptDeclineReasonCode(null)).toBeNull();
    expect(formatReceiptDeclineReasonCode('')).toBeNull();
  });
});

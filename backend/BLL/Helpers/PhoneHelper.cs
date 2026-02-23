namespace BLL.Helpers
{
    /// <summary>
    /// Utility class for phone number normalization and validation
    /// </summary>
    public static class PhoneHelper
    {
        /// <summary>
        /// Normalizes Vietnamese phone numbers by converting +84 prefix to 0
        /// Examples:
        /// +84123456789 -> 0123456789
        /// +84 123456789 -> 0123456789
        /// 0123456789 -> 0123456789 (unchanged)
        /// </summary>
        /// <param name="phone">The phone number to normalize</param>
        /// <returns>Normalized phone number starting with 0</returns>
        public static string NormalizePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return phone;
            }

            // Remove all whitespace
            var normalized = phone.Trim().Replace(" ", "").Replace("-", "");

            // Convert +84 to 0
            if (normalized.StartsWith("+84"))
            {
                normalized = "0" + normalized.Substring(3);
            }
            else if (normalized.StartsWith("84") && !normalized.StartsWith("0"))
            {
                // Handle cases like "84123456789" without the +
                normalized = "0" + normalized.Substring(2);
            }

            return normalized;
        }

        /// <summary>
        /// Validates Vietnamese phone number format
        /// Note: Phone numbers with +84 prefix are accepted as input but must be normalized first.
        /// This method validates the normalized format only.
        /// Valid format: 0XXXXXXXXX (10 digits starting with 0)
        /// </summary>
        /// <param name="phone">The phone number to validate (should be normalized first)</param>
        /// <returns>True if valid, false otherwise</returns>
        public static bool IsValidVietnamesePhone(string phone)
        {
            if (string.IsNullOrWhiteSpace(phone))
            {
                return false;
            }

            var normalized = NormalizePhone(phone);

            // Vietnamese phone numbers: 10 digits starting with 0
            // Format: 0XXXXXXXXX
            if (normalized.Length == 10 && normalized.StartsWith("0"))
            {
                return normalized.All(char.IsDigit);
            }

            return false;
        }
    }
}

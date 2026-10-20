using System;

namespace sma.plan
{
	// Every user-facing error the API sends, in one place. Each key must match
	// an entry in the frontend's src/locales/<language>/serverErrors.json (under
	// "serverErrors"); a key the frontend doesn't have yet shows the English
	// Message instead. Same meaning, same key - different meaning, different key,
	// even when the English happens to read the same.
	internal static class Errors
	{
		internal static class Common
		{
			public static readonly ApiError InternalError =
				new("common.internalError", "Internal server error.");
		}

		internal static class Auth
		{
			public static readonly ApiError NotSignedIn =
				new("auth.notSignedIn", "Not signed in.");

			public static readonly ApiError AdminRequired =
				new("auth.adminRequired", "Administrator access is required.");

			public static readonly ApiError NotAvailable =
				new("auth.notAvailable", "This action is not available.");
		}

		internal static class TimeEntries
		{
			public static readonly ApiError NotAssigned =
				new("timeEntries.notAssigned", "You are not assigned to that sub-task.");

			public static readonly ApiError NotYours =
				new("timeEntries.notYours", "That entry belongs to another team member.");
		}

		internal static class Holidays
		{
			public static readonly ApiError DateRequired =
				new("holidays.dateRequired", "A date is required.");

			public static readonly ApiError NameRequired =
				new("holidays.nameRequired", "A holiday name is required.");

			// The date goes to the frontend as "2026-12-25".
			public static ApiError DateTaken(DateOnly date) =>
				new("holidays.dateTaken",
					$"There's already a holiday on {date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.",
					new { date = date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) });

			public static readonly ApiError NotFound =
				new("holidays.notFound", "That holiday no longer exists.");
		}

		internal static class Expenses
		{
			public static readonly ApiError SignInRequired =
				new("expenses.signInRequired", "Sign in to see and submit expenses.");

			public static readonly ApiError SendAsFormWithReceipt =
				new("expenses.sendAsFormWithReceipt", "Send the expense as a form with its receipt attached.");

			public static readonly ApiError SendAsForm =
				new("expenses.sendAsForm", "Send the expense as a form.");

			public static readonly ApiError NotOnProject =
				new("expenses.notOnProject", "You are not assigned to that project.");

			public static readonly ApiError NotFound =
				new("expenses.notFound", "That expense no longer exists.");

			public static readonly ApiError NotYours =
				new("expenses.notYours", "That expense belongs to someone else.");

			public static readonly ApiError LockedForChange =
				new("expenses.lockedForChange", "This expense is approved and locked. Send it back to review to change it.");

			public static readonly ApiError LockedForDelete =
				new("expenses.lockedForDelete", "This expense is approved and locked. Send it back to review to delete it.");

			public static readonly ApiError AdminCantReplaceReceipt =
				new("expenses.adminCantReplaceReceipt", "Admins can't replace receipts.");

			public static readonly ApiError CantReviewOwn =
				new("expenses.cantReviewOwn", "Another admin must review your own expenses.");

			public static readonly ApiError AlreadyReimbursed =
				new("expenses.alreadyReimbursed", "This expense has been reimbursed and can't be reviewed again.");

			// The month goes to the frontend as "2026-09", so each language can
			// write it its own way; the English text spells it out.
			public static ApiError BilledOnInvoice(int number, DateOnly month) =>
				new("expenses.billedOnInvoice",
					$"This expense was billed on invoice {number} ({month.ToString("MMMM yyyy", System.Globalization.CultureInfo.InvariantCulture)}), so it can't be changed. To change it, cancel the invoice first.",
					new { number, month = month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) });

			public static readonly ApiError ChooseReviewAction =
				new("expenses.chooseReviewAction", "Choose approve, reject or back to review.");

			public static readonly ApiError RejectionReasonRequired =
				new("expenses.rejectionReasonRequired", "Give a reason for rejecting the expense.");

			public static readonly ApiError ReceiptNotFound =
				new("expenses.receiptNotFound", "That receipt no longer exists.");

			public static readonly ApiError ReceiptRequired =
				new("expenses.receiptRequired", "Attach a receipt or invoice for the expense.");

			public static readonly ApiError ReceiptTooLarge =
				new("expenses.receiptTooLarge", "The receipt is too large - it must be under 10 MB.");

			public static readonly ApiError ReceiptType =
				new("expenses.receiptType", "The receipt must be a photo or a PDF.");

			public static readonly ApiError DateRequired =
				new("expenses.dateRequired", "A date is required.");

			public static readonly ApiError DateInFuture =
				new("expenses.dateInFuture", "The date can't be in the future.");

			public static readonly ApiError ProjectRequired =
				new("expenses.projectRequired", "Select a project.");

			public static readonly ApiError SubprojectNotInProject =
				new("expenses.subprojectNotInProject", "That subproject doesn't belong to the selected project.");

			public static readonly ApiError CategoryInvalid =
				new("expenses.categoryInvalid", "Select a valid type of expense.");

			public static readonly ApiError MealNotesRequired =
				new("expenses.mealNotesRequired", "Notes are required for meals: say who it was with and why.");

			public static readonly ApiError AmountRequired =
				new("expenses.amountRequired", "Enter an amount above 0.");

			public static readonly ApiError AmountCents =
				new("expenses.amountCents", "Use dollars and cents, e.g. 19.99.");
		}
	}
}

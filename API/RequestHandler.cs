using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace sma.plan
{
    internal class RequestHandler
    {
        protected static Type GetModelType(string entity)
        {
            string className = string.Format("{0}.{1}", typeof(GeneralModel).Namespace, entity);
            Type modelType = Type.GetType(className, true, true);

            return modelType;
        }

        // The reply for a refused request - here, from SecurityMiddleware, and
        // from UserFunctions. HTTP 200 with the real code in status.code, which is
        // what the frontend expects. errorKey and errorParams let it show the
        // message in the user's language; error is the English, for anything
        // that doesn't (and for keys the frontend doesn't have yet).
        internal static object Envelope(int code, ApiError error)
        {
            return new
            {
                status = new
                {
                    code,
                    error = error.Message,
                    errorKey = error.Key,
                    errorParams = error.Params,
                },
                result = new { },
            };
        }

        // Access-controlled endpoints return this rather than throwing - Ok() would
        // catch the exception and flatten it into a generic 500, which the client
        // can't tell apart from a real failure.
        protected static OkObjectResult Forbidden(ApiError error)
        {
            return new OkObjectResult(Envelope(403, error));
        }

        // The request is well-formed but its data breaks a business rule.
        protected static OkObjectResult Invalid(ApiError error)
        {
            return new OkObjectResult(Envelope(400, error));
        }

        // For messages not yet in Errors: sent with no key, so the frontend shows
        // the English as it always has. Remove once every message has a key.
        protected static OkObjectResult Forbidden(string message)
        {
            return Forbidden(new ApiError(null, message));
        }

        protected static OkObjectResult Invalid(string message)
        {
            return Invalid(new ApiError(null, message));
        }

        protected static OkObjectResult Forbidden()
        {
            return Forbidden(Errors.Auth.AdminRequired);
        }

        // Time may only be booked against a sub-task the person is assigned to.
        protected static OkObjectResult NotAssigned()
        {
            return Forbidden(Errors.TimeEntries.NotAssigned);
        }

        // An existing record belongs to someone else.
        protected static OkObjectResult NotYours()
        {
            return Forbidden(Errors.TimeEntries.NotYours);
        }

        // A query-string value (?projectId=...), or null when it's left out or
        // blank.
        protected static string QueryValue(HttpRequest req, string name)
        {
            string value = req.Query[name];
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        // ?from= and ?to= as dates (yyyy-MM-dd), each null when left out, so a
        // list can be narrowed to a period. Null when they're fine, otherwise
        // the reason they aren't.
        protected static ApiError ReadDateRange(HttpRequest req, out DateOnly? from, out DateOnly? to)
        {
            from = null;
            to = null;

            foreach (string name in new[] { "from", "to" })
            {
                string text = QueryValue(req, name);
                if (text == null)
                {
                    continue;
                }
                if (!DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly date))
                {
                    return Errors.Common.DateInvalid;
                }
                if (name == "from")
                {
                    from = date;
                }
                else
                {
                    to = date;
                }
            }

            return from > to ? Errors.Common.DateRangeReversed : null;
        }

        protected static OkObjectResult Ok(Func<object> repoRequestFunc, Func<object, object> resultFormatFunc)
        {
            try
            {
                object res = repoRequestFunc();
                return new OkObjectResult(new
                {
                    status = new
                    {
                        code = 200,
                        error = "",
                    },
                    result = resultFormatFunc(res),
                });
            }

            catch (Exception ex)
            {
                System.Console.WriteLine(ex.ToString());
                return new OkObjectResult(Envelope(500, Errors.Common.InternalError));
            }
        }
    }
}

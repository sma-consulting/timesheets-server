using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
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

        // Access-controlled endpoints return this rather than throwing - Ok() would
        // catch the exception and flatten it into a generic 500, which the client
        // can't tell apart from a real failure.
        protected static OkObjectResult Forbidden(string message)
        {
            return new OkObjectResult(new
            {
                status = new
                {
                    code = 403,
                    error = message,
                },
                result = new { },
            });
        }

        protected static OkObjectResult Forbidden()
        {
            return Forbidden("Administrator access is required.");
        }

        // Time may only be booked against a sub-task the person is assigned to.
        protected static OkObjectResult NotAssigned()
        {
            return Forbidden("You are not assigned to that sub-task.");
        }

        // The request is well-formed but its data breaks a business rule.
        protected static OkObjectResult Invalid(string message)
        {
            return new OkObjectResult(new
            {
                status = new
                {
                    code = 400,
                    error = message,
                },
                result = new { },
            });
        }

        // An existing record belongs to someone else.
        protected static OkObjectResult NotYours()
        {
            return Forbidden("That entry belongs to another team member.");
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
                return new OkObjectResult(new
                {
                    status = new
                    {
                        code = 500,
                        error = "Internal server error.",
                    },
                    result = new { },
                });
            }
        }
    }
}

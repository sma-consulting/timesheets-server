using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

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

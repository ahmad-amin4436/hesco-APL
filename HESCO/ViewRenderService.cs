using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.IO;
using System.Threading.Tasks;

public class ViewRenderService
{
    private readonly ICompositeViewEngine _viewEngine;
    private readonly ITempDataProvider _tempDataProvider;
    private readonly IServiceProvider _serviceProvider;

    public ViewRenderService(ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, IServiceProvider serviceProvider)
    {
        _viewEngine = viewEngine;
        _tempDataProvider = tempDataProvider;
        _serviceProvider = serviceProvider;
    }

    public async Task<string> RenderViewToStringAsync<TModel>(ControllerContext context, string viewName, TModel model)
    {
        var viewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
        {
            Model = model
        };

        using (var writer = new StringWriter())
        {
            var viewResult = _viewEngine.FindView(context, viewName, false);

            if (viewResult.View == null)
            {
                throw new ArgumentNullException($"{viewName} does not match any available view");
            }

            var viewContext = new ViewContext(
                context,
                viewResult.View,
                viewData,
                new TempDataDictionary(context.HttpContext, _tempDataProvider),
                writer,
                new HtmlHelperOptions()
            );

            await viewResult.View.RenderAsync(viewContext);
            return writer.GetStringBuilder().ToString();
        }
    }
}

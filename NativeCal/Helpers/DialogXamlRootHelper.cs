using System;
using Microsoft.UI.Xaml;

namespace NativeCal.Helpers;

/// <summary>
/// Resolves a valid XamlRoot for dialogs in pages whose root content is rehosted
/// directly in the main window instead of keeping the Page itself in the visual tree.
/// In that setup, Page.XamlRoot can be null even though child elements are visible.
/// </summary>
public static class DialogXamlRootHelper
{
    public static XamlRoot Resolve(params FrameworkElement?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (candidate?.XamlRoot is XamlRoot root)
                return root;
        }

        if (App.MainAppWindow?.Content?.XamlRoot is XamlRoot mainRoot)
            return mainRoot;

        throw new InvalidOperationException("Unable to resolve a XamlRoot for the dialog.");
    }
}

// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// aggressively encodes a piece of text to be placed into a URL.
// is not guaranteed to be reversible so best used just for aesthetic reasons.
function EncodeParamForUrl(parameter)
{
    return parameter.replace(/-/g, "_").replace(/ /g, "-").replace(/\(/g, "").replace(/\)/g, "").toLowerCase();
}

// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// aggressively encodes a piece of text to be placed into a URL.
// is not guaranteed to be reversible so best used just for aesthetic reasons.
function EncodeParamForUrl(parameter)
{
    // remove some characters
    parameter = parameter.replace(/\(|\)/g, "");

    // replace others with hyphens
    parameter = parameter.replace(/-| |\./g, "-");

    // then just lower case it
    return parameter.toLowerCase();
}

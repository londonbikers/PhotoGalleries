// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// aggressively encodes a piece of text to be placed into a URL.
// is not guaranteed to be reversible so best used just for aesthetic reasons.
function EncodeParamForUrl(parameter)
{
    // remove some characters
    parameter = parameter.replace(/\(|\)|'|@|#/g, "");

    // replace hyphens with underscores
    parameter = parameter.replace("-", "_");

    // replace others with hyphens
    parameter = parameter.replace(/-| |\.|\//g, "-");

    // make sure we haven't doubled up hyphens
    parameter = parameter.replace(/-{2,}/g, "-");

    // make sure we don't have any leading or trailing hyphens either
    parameter = parameter.replace(/^-|-$/g, "");

    // then just lower case it
    return parameter.toLowerCase();
}

function NavigateToImage(galleryId, imageId, name) {
    window.location.href = `/i/${galleryId}/${imageId}/${EncodeParamForUrl(name)}`;
}

// for high-dpi displays we need to request a larger image than the space we intend to view it in.
// this ensures images are as crisp as they can be for each client device.
function GetImageThumbnailUrl(files, element) {
    var cardInnerWidth = $(element).parent().parent().innerWidth();
    var cardInnerHeight = Math.round(cardInnerWidth / 1.52); // 1.52 is the ratio of height to width we'd like to show the image at
    var scaledWidth = Math.round(cardInnerWidth * window.devicePixelRatio);
    var scaledHeight = Math.round(cardInnerHeight * window.devicePixelRatio);

    // choose ImageFileSpec for scaled dimensions
    if (scaledWidth <= 800 && scaledHeight <= 800 && files.Spec800Id !== null) {
        return `/di800/${files.Spec800Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else if (scaledWidth <= 1920 && scaledHeight <= 1920 && files.Spec1920Id !== null) {
        return `/di1920/${files.Spec1920Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else if (scaledWidth <= 2560 && scaledHeight <= 2560 && files.Spec2560Id !== null) {
        return `/di2560/${files.Spec2560Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else if (scaledWidth <= 3840 && scaledHeight <= 3840 && files.Spec3840Id !== null) {
        return `/di3840/${files.Spec3840Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else {
        return `/dio/${files.OriginalId}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    }
}

function GetGalleryUrl(categoryName, galleryId, name) {
    var encodedName = EncodeParamForUrl(name);
    var encodedCategoryName = EncodeParamForUrl(categoryName);
    return `/g/${encodedCategoryName}/${galleryId}/${encodedName}`;
}
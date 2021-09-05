// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// --[ GLOBAL VARIABLES ]----------------------------------------------------------------------------------

var _longDateFormat = "Do MMMM YYYY, hh:mm";

// -- [ FUNCTIONS ]----------------------------------------------------------------------------------------

function DoesBrowserSupportWebP() {
    //console.log("DoesBrowserSupportWebP()");
    var webpTested = false;
    var webpSupported = true;

    if (sessionStorage) {
        const sessionItem = sessionStorage.getItem("webpsupport");
        if (sessionItem != undefined) {
            //console.log(`DoesBrowserSupportWebP(): got webpsupport session item: ${sessionItem}`);
            webpTested = true;
            webpSupported = (sessionItem === "true");
        }
    }

    if (!webpTested) {
        // pre-version 11 macOS doesn't support webp.
        // caniuse.com states Safari version 14 required, but you can have new Safari on old macOS and this won't work.
        // some os' don't have a value for platform.os.version.
        if (typeof platform !== "undefined" && platform.os.version) {
            const osMainVersion = platform.os.version.substring(0, platform.os.version.indexOf("."));
            if (platform.os.family === "OS X" && osMainVersion < 11 && platform.name === "Safari") {
                //console.log("DoesBrowserSupportWebP(): No, because of old macOS");
                webpSupported = false;
            }
        }

        sessionStorage.setItem("webpsupport", webpSupported);
    }

    //console.log(`DoesBrowserSupportWebP(): webpSupported=${webpSupported}`);
    return webpSupported;
}

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

function GetCategoryUrl(categoryName) {
    const encodedCategoryName = EncodeParamForUrl(categoryName);
    return `/c/${encodedCategoryName}`;
}

function GetGalleryUrl(categoryName, galleryId, name) {
    const encodedName = EncodeParamForUrl(name);
    const encodedCategoryName = EncodeParamForUrl(categoryName);
    return `/g/${encodedCategoryName}/${galleryId}/${encodedName}`;
}

function GetImageUrl(galleryId, imageId, name) {
    const encodedName = EncodeParamForUrl(name);
    return `/i/${galleryId}/${imageId}/${encodedName}`;
}

// for high-dpi displays we need to request a larger image than the space we intend to view it in.
// this ensures images are as crisp as they can be for each client device.
function GetImageThumbnailUrl(files, element) {
    const cardInnerWidth = $(element).parent().parent().innerWidth();
    const cardInnerHeight = Math.round(cardInnerWidth / 1.52); // 1.52 is the ratio of height to width we'd like to show the image at
    const scaledWidth = Math.round(cardInnerWidth * window.devicePixelRatio);
    const scaledHeight = Math.round(cardInnerHeight * window.devicePixelRatio);
    const doesBrowserSupportWebP = DoesBrowserSupportWebP();

    //console.log(`GetImageThumbnailUrl(): doesBrowserSupportWebP type: ${typeof (doesBrowserSupportWebP)}`);
    //console.log(`GetImageThumbnailUrl(): doesBrowserSupportWebP=${doesBrowserSupportWebP}`);

    // our pre-generated images use the WebP format. Some old browsers don't support
    // this, so for these, just return the original image as a fall-back.
    if (doesBrowserSupportWebP === false) {
        //console.log("GetImageThumbnailUrl(): returning first dio");
        return `/dio/${files.OriginalId}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    }

    // choose ImageFileSpec for scaled dimensions
    if (scaledWidth <= 800 && scaledHeight <= 800 && files.Spec800Id !== null) {
        //console.log("GetImageThumbnailUrl(): returning di800");
        return `/di800/${files.Spec800Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else if (scaledWidth <= 1920 && scaledHeight <= 1920 && files.Spec1920Id !== null) {
        //console.log("GetImageThumbnailUrl(): returning di800");
        return `/di1920/${files.Spec1920Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else if (scaledWidth <= 2560 && scaledHeight <= 2560 && files.Spec2560Id !== null) {
        //console.log("GetImageThumbnailUrl(): returning di800");
        return `/di2560/${files.Spec2560Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else if (scaledWidth <= 3840 && scaledHeight <= 3840 && files.Spec3840Id !== null) {
        //console.log("GetImageThumbnailUrl(): returning di800");
        return `/di3840/${files.Spec3840Id}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    } else {
        //console.log("GetImageThumbnailUrl(): returning second dio");
        return `/dio/${files.OriginalId}?w=${scaledWidth}&h=${scaledHeight}&mode=crop`;
    }
}

function IsTouchDevice()
{
    if ("ontouchstart" in window || window.TouchEvent)
        return true;

    if (window.DocumentTouch && document instanceof DocumentTouch)
        return true;

    const prefixes = ["", "-webkit-", "-moz-", "-o-", "-ms-"];
    const queries = prefixes.map(prefix => `(${prefix}touch-enabled)`);

    return window.matchMedia(queries.join(",")).matches;
}

function IsMobileDevice() {
    // needs to be touch-enabled and the resolution below a certain size, i.e. don't include touch-enabled laptops
    // as it's far more likely users will want to use touch-pad/keyboard/mouse to navigate images on those devices
    // of course the weakness with this determination is that mobile device screen sizes are likely to get bigger 
    // over time, so this will need to be updated now and then.

    const isScreenMobileDeviceSize = window.screen.width <= 1366 && window.screen.height <= 1366;
    return IsTouchDevice() && isScreenMobileDeviceSize;
}

function GetBackgroundImage(image) {
    if (image.LowResStorageId !== null) {
        return `url(/dilr/${image.Files.SpecLowResId})`;
    }
    return null;
}

function AddTagToCsv(tags, tag) {
    if (tags === undefined || tags === null || tags.length === 0)
        return tag;

    const array = tags.split(",");
    array.push(tag);
    return array.join(",");
}

function RemoveTagFromCsv(tags, tag) {
    if (tags === undefined || tags === null || tags.length === 0)
        return null;

    const array = tags.split(",");
    const newTags = array.filter(function(value, index, arr) {
        return value !== tag;
    });
    return newTags.join(",");
}

// determines if a tag csv contains a specific tag.
// better than a string contains() check as it looks for exact matches, not partial.
function TagsCsvContains(tags, tag) {
    if (tags === undefined || tags === null || tags.length === 0)
        return false;

    const array = tags.split(",");
    return array.includes(tag);
}

// converts a date to ticks, making it easier to transfer dates (as just numbers) to the API, avoiding any querystring encoding issues with normal datetime characters.
function DateToTicks(date) {
    const dateObj = new Date(date);
    return ((dateObj.getTime() * 10000) + 621355968000000000);
}

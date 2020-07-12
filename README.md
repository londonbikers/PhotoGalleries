# PhotoGalleries

A website to showcase the https://londonbikers.com photo galleries, both old and new. Currently in development.

Planned tech stack:

* ASP.NET Core MVC
* Azure Cosmos DB
* Azure Blob storage
* Azure CDN
* ImageFlow (dynamic image resizing - https://www.imageflow.io)
* Azure Web App hosting
* Knockout.js

Planned features:

* Photo galleries
* Gallery categories
* Photo Metadata display
* Discourse API integration to allow cross-posting onto londonbikers.com
* Photo user comments
* Gallery user comments
* OpenID Connect federation with our IdentityServer IDP

Photos will need migrating from both our archive and the current LB photos website.

** Website Structure

/ (latest galleries + intro)
/categories (list of categories)
/categories/{name}
/galleries/{id}/{name} (gallery detail)
/admin
/admin/categories (categories list)
/admin/categories/new
/admin/categories/{id}
/admin/galleries (gallery list)
/admin/galleries/new
/admin/galleries/{id} (gallery detail)
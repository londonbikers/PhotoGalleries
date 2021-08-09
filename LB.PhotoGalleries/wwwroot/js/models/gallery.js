function GalleryModel(data) {
    const self = this;
    self.Id = ko.observable(data.Id);
    self.Created = ko.observable(data.Created);
}
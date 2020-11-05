function ImageModel(data) {
    var self = this;
    self.Id = data.Id;
    self.GalleryId = data.GalleryId;
    self.Name = ko.observable(data.Name);
    self.Width = data.Width;
    self.Height = data.Height;
    self.Files = new Object();
    self.Files.Spec800Id = data.Files.Spec800Id;
    self.Files.Spec1920Id = data.Files.Spec1920Id;
    self.Files.Spec2560Id = data.Files.Spec2560Id;
    self.Files.Spec3840Id = data.Files.Spec3840Id;
    self.Files.OriginalId = data.Files.OriginalId;
    self.Tags = ko.observableArray(data.Tags);
}

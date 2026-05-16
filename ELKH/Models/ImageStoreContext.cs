using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ELKH.Models;

public partial class ImageStoreContext : DbContext
{
    public ImageStoreContext()
    {
    }

    public ImageStoreContext(DbContextOptions<ImageStoreContext> options)
        : base(options)
    {
    }

    public virtual DbSet<ImageModel> Images { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ImageModel>(entity =>
        {
            entity.ToTable("Image");

            entity.Property(e => e.ImageId).HasColumnName("imageId");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.FileName).HasColumnName("fileName");
            entity.Property(e => e.FileType).HasColumnName("fileType");
            entity.Property(e => e.ImageData).HasColumnName("imageData");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

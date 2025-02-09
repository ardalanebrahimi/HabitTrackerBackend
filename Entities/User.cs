using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;
using Microsoft.AspNetCore.Identity;

[Table("user")]
public class User : IdentityUser<Guid>
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("email")]
    public string Email { get; set; }

    [Column("passwordhash")]
    public string PasswordHash { get; set; }

    [Column("name")]
    public string UserName { get; set; }
}

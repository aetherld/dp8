using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using StackExchange.Redis;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Valuator.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly IDatabase _redisDb;

        public RegisterModel(IConnectionMultiplexer redis)
        {
            _redisDb = redis.GetDatabase();
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(20, MinimumLength = 3)]
            public string Username { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [StringLength(100, MinimumLength = 6)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Пароли не совпадают")]
            public string ConfirmPassword { get; set; }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            if (await _redisDb.KeyExistsAsync($"user:{Input.Username}"))
            {
                ModelState.AddModelError(string.Empty, "Пользователь с таким именем уже существует");
                return Page();
            }

            var hashedPassword = HashPassword(Input.Password);
            await _redisDb.StringSetAsync($"user:{Input.Username}", hashedPassword);

            // Добавляем автоматический вход
            var claims = new List<Claim>
            {
                    new Claim(ClaimTypes.Name, Input.Username),
                    new Claim(ClaimTypes.NameIdentifier, Input.Username)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(new ClaimsPrincipal(claimsIdentity));

            return RedirectToPage("/Index");
        }

        private string HashPassword(string password)
        {
            // Генерация соли
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            // Хеширование с использованием PBKDF2
            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            // Сохраняем соль и хеш вместе
            return $"{Convert.ToBase64String(salt)}:{hashed}";
        }
    }
}
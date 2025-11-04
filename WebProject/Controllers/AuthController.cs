using FirebaseAdmin.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace WebProject.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public AuthController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public class FirebaseSignInRequest
        {
            public string Token { get; set; }
        }

        [HttpPost("signin-microsoft")]
        public async Task<IActionResult> SignInWithMicrosoft([FromBody] FirebaseSignInRequest request)
        {
            try
            {
                FirebaseToken decodedToken = await FirebaseAuth.DefaultInstance
                    .VerifyIdTokenAsync(request.Token);
                
                if (!decodedToken.Claims.TryGetValue("email", out var emailObj))
                {
                    return BadRequest(new { error = "Ваш акаунт Microsoft не надав email." });
                }
                
                string email = emailObj.ToString();
                string uid = decodedToken.Uid;

                var user = await _userManager.FindByEmailAsync(email);

                if (user == null)
                {
                    user = new IdentityUser { Email = email, UserName = email, EmailConfirmed = true };
                    var createUserResult = await _userManager.CreateAsync(user);
                    
                    if (!createUserResult.Succeeded)
                    {
                        var errors = string.Join(", ", createUserResult.Errors.Select(e => e.Description));
                        return BadRequest(new { error = $"Не вдалося створити локального користувача: {errors}" });
                    }
                    
                    await _userManager.AddLoginAsync(user, new UserLoginInfo("Firebase", uid, "Firebase"));
                }

                await _signInManager.SignInAsync(user, isPersistent: false);

                return Ok(new { message = "Вхід успішний" });
            }
            catch (FirebaseAuthException ex)
            {
                return Unauthorized(new { error = $"Недійсний токен: {ex.Message}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Внутрішня помилка: {ex.Message}" });
            }
        }
    }
}
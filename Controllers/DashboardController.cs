using CloudinaryDotNet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using RYT.Commons;
using RYT.Data;
using RYT.Models.Entities;
using RYT.Models.Enums;
using RYT.Models.ViewModels;
using RYT.Services.CloudinaryService;
using RYT.Services.Emailing;
using RYT.Services.Payment;
using RYT.Services.NotificationSaga;
using RYT.Services.Repositories;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory;


namespace RYT.Controllers
{
    [Authorize()]
    public class DashboardController : Controller
    {
        private readonly IPayments _payments;
        private readonly IRepository _repository;
        private readonly IPhotoService _photoService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IFirebaseService _firebaseService;

        public DashboardController(IFirebaseService firebaseService, IPayments payments, IRepository repository, UserManager<AppUser> userManager, IPhotoService photoService,
            IEmailService emailService)
        {
            _repository = repository;
            _userManager = userManager;
            _photoService = photoService;
            _emailService = emailService;
            _payments = payments;
            _firebaseService = firebaseService;
        }

        public async Task<IActionResult> Overview(string? tableToShow, string? msg)
        {
            decimal currentUserWalletBalance = 0.0M;
            string status = "";
            decimal amountSent = 0.0M;
            decimal amountReceived = 0.0M;
            var userTransactions = new List<Transaction>();

            var user = await _userManager.GetUserAsync(User);
            var roles = await _userManager.GetRolesAsync(user);

            var model = new OverviewViewModel
            {
                Balance = currentUserWalletBalance,
                Status = status,
                AmountReceived = amountReceived,
                AmountSent = amountSent
            };

            if (!string.IsNullOrEmpty(msg))
            {
                ViewBag.FundErrMsg = msg;
                return View(model);
            }

            if (string.IsNullOrEmpty(tableToShow))
            {
                if (roles.Any(x => x.ToLower().Equals("student")))
                {
                    ViewBag.TableToShow = tableToShow = "fund";
                }
                if (roles.Any(x => x.ToLower().Equals("teacher")))
                {
                    ViewBag.TableToShow = tableToShow = "received";
                }
            }
            else
            {
                ViewBag.TableToShow = tableToShow;
            }

            var wallets = await _repository.GetAsync<Wallet>();
            if (wallets.Any())
            {
                var userWallet = wallets.First(x => x.UserId == user.Id);
                if (userWallet != null)
                {
                    currentUserWalletBalance = userWallet.Balance;
                    status = userWallet.Status.ToString();
                }

                if (roles.Any(x => x.ToLower().Equals("teacher")))
                {
                    var transactions = await _repository.GetAsync<Transaction>();
                    if (transactions.Any())
                    {
                        userTransactions = transactions.Where(
                            x => x.TransactionType.ToLower().Equals(TransactionTypes.Transfer.ToString().ToLower()) &&
                            x.ReceiverId.Equals(user.Id) &&
                            x.Status == true
                            ).ToList();

                        userTransactions.ForEach(x =>
                        {
                            amountReceived += x.Amount;
                        });
                    }
                    if (tableToShow.ToLower().Equals("received"))
                    {
                        model.MyReceivedTransactions = userTransactions.Select(x => new ReceivedTransactionsViewModel
                        {
                            Description = x.Description,
                            Amount = x.Amount,
                            timeOfTransaction = x.UpdatedOn
                        }).ToList();
                    }
                    else
                    {
                        if (transactions.Any())
                        {
                            userTransactions = transactions.Where(
                                x => x.TransactionType.ToLower().Equals(TransactionTypes.Withdrawal.ToString().ToLower()) &&
                                x.SenderId.Equals(user.Id) &&
                                x.Status == true
                                ).ToList();
                        }
                        model.MySentTransactions = userTransactions.Select(x => new SentTransactionViewModel
                        {
                            Description = x.Description,
                            Amount = x.Amount,
                            timeOfTransaction = x.UpdatedOn
                        }).ToList();
                    }


                }

                if (roles.Any(x => x.ToLower().Equals("student")))
                {
                    var transactions = await _repository.GetAsync<Transaction>();
                    if (transactions.Any())
                    {
                        userTransactions = transactions.Include(x => x.Sender)
                            .Where(
                                x => x.TransactionType.ToLower().Equals(TransactionTypes.Transfer.ToString().ToLower()) &&
                                x.SenderId.Equals(user.Id) &&
                                x.Status == true
                            ).ToList();

                        userTransactions.ForEach(x =>
                        {
                            amountSent += x.Amount;
                        });
                    }
                    if (tableToShow.ToLower().Equals("fund"))
                    {
                        if (transactions.Any())
                        {
                            userTransactions = transactions.Where(
                                x => x.TransactionType.ToLower().Equals(TransactionTypes.Funding.ToString().ToLower()) &&
                                x.SenderId.Equals(user.Id) &&
                                x.Status == true
                                ).ToList();
                        }
                        model.MyFundings = userTransactions.Select(x => new FundingTransactionHistoryViewModel
                        {
                            Description = x.Description,
                            Amount = x.Amount,
                            CreatedOn = x.UpdatedOn
                        }).ToList();
                    }
                    else
                    {
                        model.MyTransferTransactions = userTransactions.Select(x => new TransferTransactionHistoryViewModel
                        {
                            School = "",
                            NameOfTeacher = $"{_userManager.FindByIdAsync(x.ReceiverId).Result.FirstName} {_userManager.FindByIdAsync(x.ReceiverId).Result.LastName}",
                            Amount = x.Amount,
                            dateTime = x.UpdatedOn
                        }).ToList();
                    }

                }
            }



            //if (roles.Any(x => x.ToLower().Equals("teacher")))
            //{

            //}

            //if (roles.Any(x => x.ToLower().Equals("student")))
            //{

            //}
            model.Balance = currentUserWalletBalance;
            model.Status = status;
            model.AmountReceived = amountReceived;
            model.AmountSent = amountSent;
            return View(model);
        }


        [HttpGet]
        public async Task<IActionResult> Messages(string? searchTerm, string? threadId)

        {
            MVModel messageViewModel = new MVModel();
            var loggedInUser = await _userManager.GetUserAsync(User);

            var recieverId = "";

            if (!string.IsNullOrEmpty(threadId))
            {
                recieverId = (await _repository.GetAsync<UserChat>())
                .Where(message => message.ThreadId == threadId).Select(x => x.ReceiverId).First();
                var receiver = await _userManager.FindByIdAsync(recieverId);

                var chatHistory = await _repository.GetAsync<UserChat>();

                if (chatHistory != null)
                {
                    var chats = (await _repository.GetAsync<UserChat>())
                        .Where(message => message.ThreadId == threadId)
                        .Select(message => new MessageThread
                        {
                            PhotoUrl = message.Sender.PhotoUrl,
                            Text = message.Text,
                            UserId = message.Sender.Id,
                            TimeStamp = message.TimeStamp.ToLocalTime().ToString()
                        })
                        .ToList();

                    messageViewModel.MessageThreads = chats;
                    messageViewModel.CurrentThreadInUse = threadId;
                    messageViewModel.receiverName = $"{receiver.FirstName} {receiver.LastName}";
                }
                else
                {
                    ViewBag.Msg = "No Message found";

                    return View("Messages");
                }
                messageViewModel.ReceiverId = receiver.Id;

            }

            var messages = await _repository.GetAsync<UserChat>();
            if (messages != null && messages.Any())
            {
                var filtered = messages.Where(x => x.SenderId == loggedInUser.Id || x.ReceiverId == loggedInUser.Id);
                if (filtered.Any())
                {
                    var results = filtered.ToList().GroupBy(x => x.ThreadId);
                    foreach (var messageThreads in results)
                    {
                        var last = messageThreads.OrderBy(x => x.UpdatedOn).Last();
                        var user = await _userManager.FindByIdAsync(last.SenderId);
                        messageViewModel.SideThreads.Add(new MessageViewModel
                        {
                            PhotoUrl = user.PhotoUrl,
                            LastText = last.Text,
                            ThreadId = last.ThreadId,
                            UserId = last.SenderId,
                            Name = $"{user.FirstName} {user.LastName}",
                            TimeStamp = last.UpdatedOn,
                            ReadOn = last.ReadOn,
                            DeliverOn = last.DeliveredOn
                        });
                    }
                    messageViewModel.SideThreads.OrderByDescending(x => x.TimeStamp);
                    messageViewModel.SenderId = loggedInUser.Id;
                    return View(messageViewModel);
                }
            }

            ViewBag.Msg = "No messages found";

            return View();
        }



        [HttpPost]
        public async Task<IActionResult> CreateMessage(MVModel model, string threadId)
        {
            if (!string.IsNullOrEmpty(model.SenderId) &&
                !string.IsNullOrEmpty(model.ReceiverId) &&
                !string.IsNullOrEmpty(model.NewChat) &&
                !string.IsNullOrEmpty(threadId))
            {
                await _repository.AddAsync(new UserChat
                {
                    Text = model.NewChat,
                    SenderId = model.SenderId,
                    ReceiverId = model.ReceiverId,
                    DeliveredOn = DateTime.UtcNow,
                    ReadOn = DateTime.UtcNow,
                    TimeStamp = DateTime.UtcNow,
                    ThreadId = threadId
                });
            }
            return RedirectToAction("Messages", new { threadId });
        }



        [HttpGet]
        public IActionResult SendReward(string? searchString, int page = 1)
        {
            int pageSize = 5;

            IQueryable<string> schools = SeedData.Schools.AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                schools = schools.Where(s => s.IndexOf(searchString, StringComparison.OrdinalIgnoreCase) == 0);
            }

            List<string> schoolsOnPage;
            int totalItems, totalPages;

            Pagination.UsePagination(schools, page, pageSize, out schoolsOnPage, out totalItems, out totalPages);

            var model = new ListOfSchoolViewModel();
            model.Schools = schoolsOnPage;
            model.CurrentPage = page;
            model.TotalPages = totalPages;
            model.Count = totalItems;
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SendReward(string userId, decimal amount)
        {
            var user = await _userManager.GetUserAsync(User);
            var senderId = user.Id;

            if (amount != null)
            {
                var receiverId = userId;
                var user_Receiver = await _repository.GetAsync<AppUser>(receiverId);
                var senderWallet = await (await _repository.GetAsync<Wallet>()).FirstOrDefaultAsync(x => x.UserId == senderId);
                var receiverWallet = await (await _repository.GetAsync<Wallet>()).FirstOrDefaultAsync(x => x.UserId == receiverId);
                if (amount > 0)
                {
                    if (senderWallet != null)
                    {
                        if (senderWallet.Status == WalletStatus.Active)
                        {
                            if (senderWallet.Balance > 0 && senderWallet.Balance >= amount)
                            {
                                Transaction rewardTransaction = new Transaction()
                                {
                                    WalletId = senderWallet.Id,
                                    Amount = amount,
                                    SenderId = senderId,
                                    ReceiverId = receiverId,
                                    TransactionType = TransactionTypes.Transfer.ToString(),
                                    Description = $"Transfer of the sum of \u20A6{amount}  by {user.FirstName} {user.LastName}",
                                    Reference = Guid.NewGuid().ToString(),
                                    Status = true
                                };

                                using (var trnxObj = await _repository._ctx.Database.BeginTransactionAsync())
                                {
                                    try
                                    {
                                        var transactionResult = await _repository.AddAsync<Transaction>(rewardTransaction);
                                        senderWallet.Balance -= amount;
                                        receiverWallet.Balance += amount;
                                        await _repository.UpdateAsync<Wallet>(senderWallet);
                                        await _repository.UpdateAsync<Wallet>(receiverWallet);
                                        senderWallet.Transactions.Add(rewardTransaction);
                                        receiverWallet.Transactions.Add(rewardTransaction);
                                        var link = Url.Action("Login", "Account", null, Request.Scheme);
                                        var body = @$"Hi {user_Receiver.FirstName},
             Congratulations, you have been rewarded with a sum of {amount} by {user.FirstName} {user.LastName}. Kindly click <a href='{link}'>here</a> to login to your account";
                                        await _emailService.SendEmailAsync(user_Receiver.Email, "Reward Notification", body);

                                        await trnxObj.CommitAsync();
                                        return Json(new { Code = 200, name = user.FirstName + " " + user.LastName, amount = $"\u20A6{amount}" });
                                    }
                                    catch (Exception ex)
                                    {
                                        await trnxObj.RollbackAsync();
                                        Console.WriteLine(ex.ToString());
                                        var resultmessage = "Unsuccesfull Transaction";
                                        return Json(new { Code = 400, unsuccesfulTransactionResult = resultmessage });
                                    }
                                }
                            }
                            else
                            {

                                return Json(new { Code = 300, insufficientBalanceResult = "Insufficient Balance" });
                            }

                        }
                        else
                        {
                            var resultmessage = "Your Wallet is Inactive";
                            return Json(new { Code = 700, inactiveWalletResult = resultmessage });

                        }
                    }
                    else
                    {
                        var resultmessage = "User Wallet not Found";
                        return Json(new { Code = 404, userNotFoundResult = resultmessage });

                    }

                }

                else
                {
                    var resultmessage = "Invalid entry!";
                    return Json(new { Code = 600, invalidEntryResult = resultmessage });


                }
            }
            else
            {
                return Json(new { Code = 800, noAmountResult = "Enter an Amount!" });
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> EditProfile() //pass in you VM
        {
            var user = await _userManager.GetUserAsync(User);

            var editProfileViewModel = new EditProfileVM()
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                NameofSchool = user.NameofSchool
            };

            return View(editProfileViewModel);
        }

        [HttpPost]
        public async Task<IActionResult> EditProfile(EditProfileVM editProfileVM)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                user.FirstName = editProfileVM.FirstName;
                user.LastName = editProfileVM.LastName;
                user.Email = editProfileVM.Email;
                user.PhoneNumber = editProfileVM.PhoneNumber;
                user.NameofSchool = editProfileVM.NameofSchool;

                await _repository.UpdateAsync<AppUser>(user);
            }

            return RedirectToAction("Overview");
        }

        [HttpGet]
        public IActionResult ChangePassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user != null)
                {
                    var result = await _userManager.ChangePasswordAsync(user, model.OldPassword, model.NewPassword);

                    if (result.Succeeded)
                    {
                        ViewBag.PasswordChangeSuccess = "Password change was successful!";
                        return View();
                    }
                    else
                    {
                        foreach (var error in result.Errors)
                        {
                            ModelState.AddModelError(string.Empty, error.Description);
                        }

                        return View(model);
                    }
                }

                ModelState.AddModelError("", "User not found");
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Teachers(string schoolName, int page = 1)
        {
            var model = new TeacherListViewModel();
            if (!string.IsNullOrEmpty(schoolName))
            {
                int pageSize = 5;

                var listOfSchoolTaught = await _repository.GetAsync<SchoolsTaught>();


                if (listOfSchoolTaught != null && listOfSchoolTaught.Any())
                {
                    var schoolsTaught = (await _repository.GetAsync<SchoolsTaught>())
                                        .Include(x => x.Teacher)
                                        .ThenInclude(x => x.User).ToList();

                    var teachersBySchool = listOfSchoolTaught.Where(x => x.School.Equals(schoolName))
                                                             .Select(x => x.Teacher).ToList()
                                                             .Distinct();

                    var paginated = new List<Teacher>();
                    int totalItems;
                    int totalPages;

                    Pagination.UsePagination(teachersBySchool.AsQueryable(), page, pageSize, out paginated, out totalItems, out totalPages);
                    model.TeacherList = paginated;
                    model.SchoolName = schoolName;
                    model.CurrentPage = page;
                    model.Count = totalItems;
                    model.TotalPages = totalPages;

                    return View(model);
                }
            }
            else
            {
                ModelState.AddModelError("", "School name must not be empty!");
            }

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Teachers(TeacherListViewModel model, string schoolName, int page = 1)
        {
            int pageSize = 5;

            var listOfSchoolTaught = await _repository.GetAsync<SchoolsTaught>();
            if (listOfSchoolTaught != null && listOfSchoolTaught.Any())
            {
                var schoolsTaught = (await _repository.GetAsync<SchoolsTaught>())
                                    .Include(x => x.Teacher)
                                    .ThenInclude(x => x.User).ToList();

                if (!string.IsNullOrEmpty(model.SearchKeyword))
                {
                    schoolsTaught = schoolsTaught.Where(s => s.Teacher.User.FirstName.ToLower().Equals(model.SearchKeyword) ||
                                                             s.Teacher.User.LastName.ToLower().Equals(model.SearchKeyword)).ToList();
                }

                var paginated = new List<SchoolsTaught>();
                int totalItems;
                int totalPages;

                Pagination.UsePagination(schoolsTaught.AsQueryable(), page, pageSize, out paginated, out totalItems, out totalPages);

                model.TeacherList = paginated.Select(x => x.Teacher).ToList();
                model.SchoolName = model.CurrentSchool;
                model.CurrentPage = page;
                model.Count = totalItems;
                model.TotalPages = totalPages;

                return View(model);
            }

            return View(model);
        }


        public IActionResult Transfer()
        {
            return View();
        }

        public async Task<IActionResult> GetSentTransaction(string userId)
        {
            List<SentTransactionViewModel> sentTransactionViewModels = new List<SentTransactionViewModel>();
            var getTransactions = await _repository.GetAsync<Transaction>();
            List<Transaction> transactions = getTransactions.Where(transaction => transaction.SenderId == userId && transaction.TransactionType == "Sent").ToList();
            foreach (var transaction in transactions)
            {
                SentTransactionViewModel transactionsView = new SentTransactionViewModel()
                {
                    Amount = transaction.Amount,
                    timeOfTransaction = transaction.CreatedOn,
                    Description = transaction.Description
                };
                sentTransactionViewModels.Add(transactionsView);
            }
            OverviewViewModel overviewViewModel = new OverviewViewModel()
            {
                //MySentTransactions = sentTransactionViewModels
            };

            return View(overviewViewModel);
        }

        public async Task<IActionResult> GetReceivedTransaction(string userId)
        {
            List<ReceivedTransactionsViewModel> receivedTransactionViewModels = new List<ReceivedTransactionsViewModel>();
            var GetTransactions = await _repository.GetAsync<Transaction>();
            List<Transaction> transactions = GetTransactions.Where(transaction => transaction.ReceiverId == userId && transaction.TransactionType == "Received").ToList();
            foreach (var transaction in transactions)
            {
                ReceivedTransactionsViewModel transactionsView = new ReceivedTransactionsViewModel()
                {
                    Amount = transaction.Amount,
                    timeOfTransaction = transaction.CreatedOn,
                    Description = transaction.Description
                };
                receivedTransactionViewModels.Add(transactionsView);
            }
            OverviewViewModel overviewViewModel = new OverviewViewModel()
            {
                MyReceivedTransactions = receivedTransactionViewModels
            };

            return View(overviewViewModel);
        }

        [HttpGet]
        public IActionResult UpdateImage()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UpdateImage(UploadImageVM model)
        {
            if (ModelState.IsValid)
            {
                //To get User
                var user = await _userManager.GetUserAsync(User);

                //To go to the database and get the AppUser properties so that we can confirm if the picture is  if (user.PhotoUrl == null)
                if (user != null)
                {
                    Dictionary<string, string> cloudinaryResponse =
                        await _photoService.UploadImage(model.Image, $"{user.LastName} {user.FirstName}");
                    if (cloudinaryResponse["Code"] == "200")
                    {
                        string photoUrl = cloudinaryResponse["Url"];
                        string publicId = cloudinaryResponse["PublicId"];
                        user.PhotoUrl = photoUrl;
                        user.PublicId = publicId;
                        var result = await _userManager.UpdateAsync(user);

                        if (result.Succeeded)
                        {
                            return RedirectToAction("UpdateImage");
                        }
                        else
                        {
                            foreach (var err in result.Errors)
                            {
                                ModelState.AddModelError(err.Code, err.Description);
                            }
                        }
                    }

                    ModelState.AddModelError("", cloudinaryResponse["Message"]);
                }
            }

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> TransferHistory(string userId)
        {
            FundAndTransferCombinedViewModel fundAndTransferCombinedViewModel = new FundAndTransferCombinedViewModel();

            IQueryable<Transaction> GetTransactions = await _repository.GetAsync<Transaction>();

            List<Transaction> transactions = GetTransactions
                .Where(transaction => transaction.TransactionType == TransactionTypes.Transfer
                .ToString() && userId == transaction.SenderId).ToList();

            foreach (Transaction transaction in transactions)
            {
                var getTeacher = await _repository.GetAsync<Teacher>();

                var getUser = await _repository.GetAsync<AppUser>();

                AppUser receiver = await _repository.GetAsync<AppUser>(transaction.ReceiverId);
                Teacher teacher = (await _repository.GetAsync<Teacher>()).FirstOrDefault(t => t.UserId == receiver.Id);

                var school = teacher.SchoolsTaughts.OrderByDescending(x => x.CreatedOn).FirstOrDefault().School;

                TransferTransactionHistoryViewModel transferTransactionHistoryViewModel
                    = new TransferTransactionHistoryViewModel()
                    {
                        NameOfTeacher = receiver.FirstName + " " + receiver.LastName,
                        Amount = transaction.Amount,
                        dateTime = transaction.CreatedOn,
                        School = school,
                    };

                fundAndTransferCombinedViewModel.TransferTransactions.Add(transferTransactionHistoryViewModel);
            }

            return View(fundAndTransferCombinedViewModel);
        }

        [HttpGet]
        public async Task<IActionResult> FundingHistory(string userId)
        {
            FundAndTransferCombinedViewModel fundAndTransferCombinedViewModel = new FundAndTransferCombinedViewModel();

            IQueryable<Transaction> GetTransactions = await _repository.GetAsync<Transaction>();

            List<Transaction> fundTransactions = GetTransactions.Where(transaction => transaction
            .SenderId == userId && transaction.TransactionType == TransactionTypes.Funding.ToString()).ToList();

            foreach (var transaction in fundTransactions)
            {
                FundingTransactionHistoryViewModel transactionsView = new FundingTransactionHistoryViewModel()
                {
                    Amount = transaction.Amount,
                    CreatedOn = transaction.CreatedOn,
                    Description = transaction.Description
                };
                fundAndTransferCombinedViewModel.FundingTransactions.Add(transactionsView);
            }

            return View(fundAndTransferCombinedViewModel);
        }
        [HttpGet]
        public async Task<IActionResult> EditTeacherProfile()
        {
            var loggedInUsedr = await _userManager.GetUserAsync(User);

            var teacher = ((await _repository.GetAsync<Teacher>())
                .Include(x => x.User)
                .Include(x => x.TeacherSubjects)
                .Include(x => x.SchoolsTaughts)
            ).FirstOrDefault(x => x.UserId == loggedInUsedr.Id);

            if (teacher != null)
            {
                var editProfileViewModel = new EditTeacherProfileVM()
                {
                    TeacherId = teacher.Id,
                    FirstName = $"{teacher.User.FirstName}",
                    LastName = $"{teacher.User.LastName}",
                    Email = teacher.User.Email,
                    PhoneNumber = teacher.User.PhoneNumber,
                    SchoolType = teacher.SchoolType,
                    YearsOfTeaching = teacher.YearsOfTeaching,
                    TeachersSubject = teacher.TeacherSubjects.Select(x => x.Subject).ToList(),
                    TeachersSchool = teacher.SchoolsTaughts.Select(x => x.School).ToList(),
                };
                return View(editProfileViewModel);
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> EditTeacherProfile(EditTeacherProfileVM model)
        {
            if (ModelState.IsValid)
            {
                if (model.SelectedSchool == "" || model.SelectedSubject == "")
                {
                    ModelState.AddModelError("", "You didn't select a school or subject");
                    return View(model);
                }

                var loggedInUsedr = await _userManager.GetUserAsync(User);
                var teacher = ((await _repository.GetAsync<Teacher>())
                   .Include(x => x.User)
                   .Include(x => x.TeacherSubjects)
                   .Include(x => x.SchoolsTaughts)
               ).FirstOrDefault(x => x.UserId == loggedInUsedr.Id);

                if (teacher != null)
                {
                    teacher.User.FirstName = model.FirstName;
                    teacher.User.LastName = model.LastName;
                    teacher.User.PhoneNumber = model.PhoneNumber;
                    teacher.YearsOfTeaching = model.YearsOfTeaching;
                    teacher.SchoolType = model.SchoolType;

                    using var startTransaction = _repository._ctx.Database.BeginTransaction();
                    try
                    {
                        await _repository.UpdateAsync(teacher);

                        foreach (var item in teacher.SchoolsTaughts.ToList())
                        {
                            await _repository.DeleteAsync(item);
                        }

                        foreach (var item in teacher.TeacherSubjects.ToList())
                        {
                            await _repository.DeleteAsync(item);
                        }

                        await _repository.AddAsync(new SchoolsTaught { TeacherId = model.TeacherId, School = model.SelectedSchool });
                        await _repository.AddAsync(new SubjectsTaught { TeacherId = model.TeacherId, Subject = model.SelectedSubject });

                        await startTransaction.CommitAsync();

                        return RedirectToAction("overview", "dashboard");
                    }
                    catch
                    {
                        await startTransaction.RollbackAsync();
                        ModelState.AddModelError("", "Edit teacher failed.");
                        return View(model);
                    }
                }

                ModelState.AddModelError("", "Could not get loggedin user record.");

            }

            return View(model);

        }


        [HttpPost]
        public async Task<IActionResult> Withdraw(string bank, decimal amt, string acc)
        {
            if (amt <= 0)
            {
                return Json(new { Code = 400, Message = "Invalid amount! Please enter an amount greaterthan zero" });
            }

            if (string.IsNullOrEmpty(bank) || bank.Equals("Select Bank"))
            {
                return Json(new { Code = 400, Message = "Please select your bank" });
            }

            int n;
            bool isNumeric = int.TryParse(acc, out n);
            if (!isNumeric || acc.Length != 10)
            {
                return Json(new { Code = 400, Message = "Please ensure you entered 10 digits for your account number!" });
            }

            var loggedInUser = await _userManager.GetUserAsync(User);

            var wallets = await _repository.GetAsync<Wallet>();
            if (wallets != null && wallets.Any())
            {
                var userWallet = wallets.FirstOrDefault(x => x.UserId == loggedInUser.Id);
                if (userWallet != null)
                {
                    if (userWallet.Balance > amt)
                    {
                        using (var trnxObj = await _repository._ctx.Database.BeginTransactionAsync())
                        {
                            Transaction rewardTransaction = new Transaction()
                            {
                                WalletId = userWallet.Id,
                                Amount = amt,
                                SenderId = loggedInUser.Id,
                                ReceiverId = loggedInUser.Id,
                                TransactionType = TransactionTypes.Withdrawal.ToString(),
                                Description = $"Withdrawal of the sum of \u20A6{amt}  has been completed for {loggedInUser.FirstName} {loggedInUser.LastName}",
                                Reference = Guid.NewGuid().ToString(),
                                Status = true
                            };
                            try
                            {
                                var transactionResult = await _repository.AddAsync<Transaction>(rewardTransaction);
                                userWallet.Balance -= amt;
                                await _repository.UpdateAsync<Wallet>(userWallet);

                                var body = @$"Hi {loggedInUser.FirstName}, Congratulations, your withdrawal of the sum of {amt} by was successful.";
                                await _emailService.SendEmailAsync(loggedInUser.Email, "Reward Notification", body);

                                await trnxObj.CommitAsync();
                                var redirectUrl = Url.Action("overview", "dashboard", new { tableToShow="sent" }, Request.Scheme);
                                return Json(new { Code = 200, name = loggedInUser.FirstName + " " + loggedInUser.LastName, amount = $"\u20A6{amt}", RtnUrl = redirectUrl });
                                //return Json(new { Code = 200, name = loggedInUser.FirstName + " " + loggedInUser.LastName, amount = $"\u20A6{amt}", newBal = userWallet.Balance });
                            }
                            catch (Exception ex)
                            {
                                await trnxObj.RollbackAsync();
                                Console.WriteLine(ex.ToString());
                                var resultmessage = "Unsuccesfull Transaction";
                                return Json(new { Code = 400, unsuccesfulTransactionResult = resultmessage });
                            }
                        }
                    }
                    else
                    {
                        return Json(new { Code = 400, Message = "Insufficient fund!" });
                    }
                }

            }


            return Json(new { Code = 400, Message = "Server error: Could not get user's wallet record!" }); ;
        }

        [HttpPost]
        public async Task<IActionResult> FundWallet(decimal amt)
        {
            decimal n;
            bool isNumeric = decimal.TryParse(amt.ToString(), out n);
            if (!isNumeric)
            {
                return RedirectToAction("overview", new { msg = "invalid fund entry!" });
            }

            if (amt < 1)
            {
                return Json(new { Code = 400, Message = "Invalid amount! Please enter an amount greaterthan zero" });
            }

            AppUser user = await _userManager.GetUserAsync(User);
            var model = new FundWalletVM { Amount = amt };
            var response = await _payments.Initialize(model, user.Id);
            return Json(new { Code = 200, Message = response.Item2 });
        }
        public async Task SendNotification()
        {
            FireBaseNotfication notification = new FireBaseNotfication
            {
                userId = "148d2f6f-af7a-423a-baa2-f01d434d9b3a",
                messages = "gdh i love him like mad",
                dateTime = DateTime.UtcNow,
            };
            int result = await _firebaseService.AddNotification(notification);
            Console.WriteLine(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetNotifications()
        {
            AppUser user = await _userManager.GetUserAsync(User);

            if (user == null)
            {

                return Content("0", "text/html");
            }
            Console.WriteLine(user.Id);
            IEnumerable<FireBaseNotfication> result = (await _firebaseService.GetNotifications(user.Id)).OrderByDescending(n => n.notificationId);
            if (result != null)
            {
                string FinalResult = "";
                foreach (FireBaseNotfication fireBaseNotfication in result)
                {
                    string html = "";
                    if (fireBaseNotfication.status == "sent")
                    {
                        html = "< div class='ogm-flex-words ogm-border-around'id ='INotifyU" + fireBaseNotfication.notificationId + "'><p>" + fireBaseNotfication.messages + "<br /> <span class='ogm -day-time'>" + fireBaseNotfication.dateTime + "</span> </p></ div >";
                    }
                    else
                    {
                        html = "<p onclick='handleNotificationClick(this)'id ='INotifyU" + fireBaseNotfication.notificationId + "'>" + fireBaseNotfication.messages + "<br> <span class='ogm-day-time pt'>" + fireBaseNotfication.dateTime + "</span> </p>";
                    }
                    FinalResult += html;
                }
                var htmlContent = FinalResult;
                return Content(htmlContent, "text/html");
            }
            return Content("No Notification", "text/html");
        }
        public async Task<IActionResult> CountUnreadNotifications()
        {
            AppUser user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Content("0", "text/html");
            }
            IEnumerable<FireBaseNotfication> getList = (await _firebaseService.GetNotifications(user.Id));
            int result = getList.Where(u => u.status == "sent").Count();
            string counterUnread = result.ToString();
            return Content(counterUnread, "text/html");
        }
        [HttpGet]
        public async Task<string> ReadNotification(string notification)
        {

            int notificationId = int.Parse(notification.Replace("INotifyU", ""));
            AppUser user = await _userManager.GetUserAsync(User);
            int result = await _firebaseService.ReadNotification(user.Id, notificationId);
            if (result == 0)
            {
                return "no";
            }
            return "yes";

        }
        public async Task DeleteNotification(string userId, int notificationId)
        {
            int result = await _firebaseService.DeleteNotification(userId, notificationId);
            if (result == 0)
            {
                Console.WriteLine("no");
            }
            Console.WriteLine("yes");
        }
    }
}


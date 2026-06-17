using ProjectDish.Core;
using ProjectDish.Services;
using System.Collections.ObjectModel;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace ProjectDish.MVVM.ViewModels
{
    class RecipeDetailsViewModel : ViewModelBase
    {
        private readonly int _recipeId;
        private readonly int _userId;
        private readonly bool _isAdmin;
        private readonly DispatcherTimer _refreshTimer;
        private bool _isBusy;
        private string _recipeName;
        private string _category;
        private string _description;
        private string _ingredients;
        private string _imageUrl;
        private decimal _averageRating;
        private int _userRating;
        private bool _isRatingPanelVisible;
        private bool _isCommentsPanelVisible;
        private string _newCommentText;
        private readonly RecipeDetailsRepository _detailsRepository;

        public ObservableCollection<CommentViewModel> Comments { get; set; } = new ObservableCollection<CommentViewModel>();
        public bool IsCommentsPanelVisible
        {
            get => _isCommentsPanelVisible;
            set { _isCommentsPanelVisible = value; OnPropertyChanged(); }
        }
        public string NewCommentText
        {
            get => _newCommentText;
            set { _newCommentText = value; OnPropertyChanged(); }
        }
        public bool CanUserPostComment => _userId > 0;
        public bool IsAdmin => _isAdmin;
        public bool IsBusy { get => _isBusy; set { _isBusy = value; OnPropertyChanged(); } }
        public string RecipeName { get => _recipeName; set { _recipeName = value; OnPropertyChanged(); } }
        public string Category { get => _category; set { _category = value; OnPropertyChanged(); } }
        public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
        public string Ingredients { get => _ingredients; set { _ingredients = value; OnPropertyChanged(); } }
        public string ImageUrl { get => _imageUrl; set { _imageUrl = value; OnPropertyChanged(); } }
        public decimal AverageRating { get => _averageRating; set { _averageRating = value; OnPropertyChanged(); } }
        public int UserRating { get => _userRating; set { _userRating = value; OnPropertyChanged(); } }
        public bool IsRatingPanelVisible { get => _isRatingPanelVisible; set { _isRatingPanelVisible = value; OnPropertyChanged(); } }
        public bool CanUserRate => !_isAdmin && _userId > 0;
        public ObservableCollection<StarViewModel> Stars { get; }
        public RelayCommand RateRecipeCommand { get; }
        public RelayCommand ToggleRatingPanelCommand { get; }
        public RelayCommand CloseCommand { get; }
        public RelayCommand ToggleCommentsPanelCommand { get; private set; }
        public RelayCommand CloseCommentsCommand { get; private set; }
        public RelayCommand PostCommentCommand { get; private set; }
        public RecipeDetailsViewModel(int recipeId, int userId, bool isAdmin)
        {
            _recipeId = recipeId;
            _userId = userId;
            _isAdmin = isAdmin;
            _detailsRepository = new RecipeDetailsRepository();
            Logger.Info($"Opening recipe details window for RecipeId: {_recipeId}, UserId: {_userId}, IsAdmin: {_isAdmin}");
            RateRecipeCommand = new RelayCommand(async (param) => await Star_ClickAsync(param), (param) => CanUserRate);
            ToggleRatingPanelCommand = new RelayCommand(o => IsRatingPanelVisible = !IsRatingPanelVisible);
            CloseCommand = new RelayCommand(o => (o as Window)?.Close());
            ToggleCommentsPanelCommand = new RelayCommand(o => IsCommentsPanelVisible = !IsCommentsPanelVisible);
            CloseCommentsCommand = new RelayCommand(o => IsCommentsPanelVisible = false);
            PostCommentCommand = new RelayCommand(async o => await PostComment(), o => CanUserPostComment && !string.IsNullOrWhiteSpace(NewCommentText));
            Stars = new ObservableCollection<StarViewModel>();
            for (int i = 1; i <= 5; i++)
            {
                Stars.Add(new StarViewModel(i));
            }
            _ = InitializeViewModel();
            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
            _refreshTimer.Tick += async (s, e) => await RefreshAllData(true);
            _refreshTimer.Start();
        }
        private async Task InitializeViewModel()
        {
            IsBusy = true;
            await RefreshAllData(false);
            IsBusy = false;
        }
        private async Task RefreshAllData(bool forceRefresh)
        {
            var detailsTask = LoadRecipeDetails(forceRefresh);
            var commentsTask = LoadComments(forceRefresh);
            await Task.WhenAll(detailsTask, commentsTask);
        }
        private async Task LoadRecipeDetails(bool forceRefresh)
        {
            var row = await _detailsRepository.GetRecipeDetailsAsync(_recipeId, forceRefresh);
            if (row == null)
            {
                if (forceRefresh) AppDialog.Show("Не удалось обновить информацию о рецепте.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            try
            {
                RecipeName = row["recipe_name"].ToString();
                Category = $"Категория: {row["category_name"]}";
                Description = row["description"].ToString();
                Ingredients = row["ingredients"].ToString();
                ImageUrl = row["image_url"] != DBNull.Value ? row["image_url"].ToString() : null;
                AverageRating = (row.Table.Columns.Contains("average_rating") && row["average_rating"] != DBNull.Value)
                    ? Convert.ToDecimal(row["average_rating"])
                    : 0m;
                if (_userId > 0 && !_isAdmin)
                {
                    var userRatingResult = await DatabaseHelper.ExecuteRpcScalarAsync("get_user_rating", new { p_user = _userId, p_recipe = _recipeId });
                    UserRating = userRatingResult.HasValue ? Convert.ToInt32(userRatingResult.Value) : 0;
                }
                RefreshStarsView();
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to parse recipe details data", ex, new { recipe_id = _recipeId });
            }
        }
        private async Task LoadComments(bool forceRefresh)
        {
            try
            {
                var commentsDt = await DatabaseHelper.ExecuteQuery("get_recipe_comments", new { p_recipe_id = _recipeId });
                var userVotes = new Dictionary<int, short>();

                if (_userId > 0)
                {
                    var votesDt = await DatabaseHelper.ExecuteQuery("get_user_votes_for_recipe_comments", new { p_user_id = _userId, p_recipe_id = _recipeId });
                    foreach (DataRow row in votesDt.Rows)
                    {
                        userVotes[Convert.ToInt32(row["comment_id"])] = Convert.ToInt16(row["vote_type"]);
                    }
                }
                var newComments = commentsDt.AsEnumerable().Select(row => {
                    var commentId = Convert.ToInt32(row["comment_id"]);
                    return new CommentViewModel
                    {
                        CommentId = commentId,
                        UserId = Convert.ToInt32(row["user_id"]),
                        UserName = row["user_name"].ToString(),
                        CommentText = row["comment_text"].ToString(),
                        CreatedAt = Convert.ToDateTime(row["created_at"]),
                        Rating = Convert.ToInt32(row["rating"]),
                        UserVote = userVotes.ContainsKey(commentId) ? userVotes[commentId] : 0,
                        VoteCommand = new RelayCommand(async p => await VoteOnComment(commentId, Convert.ToInt32(p)), p => _userId > 0),
                        DeleteCommand = new RelayCommand(async p => await DeleteComment(commentId), p => _isAdmin)
                    };
                }).ToList();

                UpdateCommentsCollection(newComments);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to load comments", ex, new { recipe_id = _recipeId });
            }
        }
        private void UpdateCommentsCollection(List<CommentViewModel> source)
        {
            var sourceDict = source.ToDictionary(c => c.CommentId);
            var targetIds = Comments.Select(c => c.CommentId).ToList();
            foreach (var id in targetIds)
            {
                if (!sourceDict.ContainsKey(id))
                {
                    Application.Current.Dispatcher.Invoke(() => Comments.Remove(Comments.First(c => c.CommentId == id)));
                }
            }
            foreach (var sourceItem in source)
            {
                var targetItem = Comments.FirstOrDefault(c => c.CommentId == sourceItem.CommentId);
                if (targetItem == null)
                {
                    Application.Current.Dispatcher.Invoke(() => Comments.Add(sourceItem));
                }
                else
                {
                    targetItem.Rating = sourceItem.Rating;
                    targetItem.UserVote = sourceItem.UserVote;
                }
            }
        }
        // Добавление комментария
        private async Task PostComment()
        {
            IsBusy = true;
            try
            {
                await DatabaseHelper.ExecuteNonQuery("add_recipe_comment", new { p_recipe_id = _recipeId, p_user_id = _userId, p_comment_text = NewCommentText });
                NewCommentText = string.Empty;
                await LoadComments(false);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to post comment", ex, new { user_id = _userId, recipe_id = _recipeId });
                AppDialog.Show("Не удалось опубликовать комментарий.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task VoteOnComment(int commentId, int voteType)
        {
            try
            {
                var newRatingResult = await DatabaseHelper.ExecuteRpcScalarAsync("vote_on_comment", new { p_comment_id = commentId, p_user_id = _userId, p_vote_type = (short)voteType });
                var comment = Comments.FirstOrDefault(c => c.CommentId == commentId);
                if (comment != null && newRatingResult.HasValue)
                {
                    comment.Rating = Convert.ToInt32(newRatingResult.Value);
                    comment.UserVote = (comment.UserVote == voteType) ? 0 : voteType;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to vote on comment", ex, new { user_id = _userId, comment_id = commentId });
            }
        }
        private async Task DeleteComment(int commentId)
        {
            var result = AppDialog.Show("Вы уверены, что хотите удалить этот комментарий?", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            IsBusy = true;
            try
            {
                await DatabaseHelper.ExecuteNonQuery("delete_recipe_comment", new { p_comment_id = commentId });
                await LoadComments(false);
            }
            catch (Exception ex)
            {
                Logger.Error("Failed to delete comment", ex, new { admin_id = _userId, comment_id = commentId });
            }
            finally
            {
                IsBusy = false;
            }
        }
        private async Task Star_ClickAsync(object param)
        {
            if (!CanUserRate)
            {
                AppDialog.Show("Оценивать рецепт могут только авторизованные пользователи.", "Ограничение", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            int selectedRating;
            if (param is int intRating)
            {
                selectedRating = intRating;
            }
            else if (param != null && int.TryParse(param.ToString(), out int parsedRating))
            {
                selectedRating = parsedRating;
            }
            else
            {
                Logger.Warn("Invalid rating parameter", new { param });
                return;
            }
            Logger.Info($"User {_userId} is rating recipe {_recipeId} with {selectedRating} stars.");
            var rpcParams = new { p_user = _userId, p_recipe = _recipeId, p_rating = selectedRating };
            try
            {
                var newAvg = await DatabaseHelper.ExecuteRpcScalarAsync("rate_recipe", rpcParams);
                if (newAvg.HasValue)
                {
                    UserRating = selectedRating;
                    AverageRating = newAvg.Value;
                    RefreshStarsView();
                    _detailsRepository.InvalidateCache(_recipeId);
                    RecipeRepository.Instance.InvalidateCache();
                    IsRatingPanelVisible = false;
                }
                else
                {
                    Logger.Warn("Failed to get new average rating.", new { recipe_id = _recipeId });
                    AppDialog.Show("Не удалось получить обновлённый рейтинг.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("Error while submitting rating", ex, new { user_id = _userId, recipe_id = _recipeId });
                AppDialog.Show($"Ошибка при отправке рейтинга: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void RefreshStarsView()
        {
            int starsToFill = UserRating;

            foreach (var star in Stars)
            {
                star.IsFilled = star.Value <= starsToFill;
            }
        }
        public void OnWindowClosing()
        {
            _refreshTimer?.Stop();
            Logger.Info($"Closing recipe details window for RecipeId: {_recipeId}");
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using Newtonsoft.Json;
using FollowMePeak.Models;
using FollowMePeak.Utils;

namespace FollowMePeak.Services
{
    /// <summary>
    /// Handles community ratings & reports: voter identity, submission via the API
    /// and a local cache of the player's own votes (share code -> stars).
    /// </summary>
    public class RatingService
    {
        private readonly ModLogger _logger;
        private readonly VPSApiService _apiService;
        private readonly string _myRatingsFilePath;

        private Dictionary<string, int> _myRatings = new Dictionary<string, int>();

        public RatingService(ModLogger logger, VPSApiService apiService)
        {
            _logger = logger;
            _apiService = apiService;
            _myRatingsFilePath = Path.Combine(Paths.PluginPath, "FollowMePeak_Data", "my_ratings.json");
            LoadMyRatings();
        }

        // Anonymous per-installation voter identity (auto-generated in BepInEx config)
        public string VoterId => Plugin.CommunityVoterId.Value;

        // Returns the player's own vote for a climb (by share code), or null if not voted
        public int? GetMyRating(string shareCode)
        {
            if (string.IsNullOrEmpty(shareCode)) return null;
            return _myRatings.TryGetValue(shareCode, out int stars) ? stars : null;
        }

        // Submits a 1-5 star rating. Callback: (success, newRatingAverage, newRatingCount)
        public void SubmitRating(ClimbData climb, int stars, Action<bool, float, int> callback)
        {
            if (climb == null || !climb.IsFromCloud)
            {
                _logger.Warning("Rating rejected: only cloud climbs can be rated");
                callback?.Invoke(false, 0f, 0);
                return;
            }

            if (stars < 1 || stars > 5)
            {
                _logger.Warning($"Rating rejected: invalid star count {stars}");
                callback?.Invoke(false, 0f, 0);
                return;
            }

            _apiService.SubmitRating(climb.Id.ToString(), VoterId, stars, (success, data, error) =>
            {
                if (success && data != null)
                {
                    climb.RatingAverage = data.RatingAvg;
                    climb.RatingCount = data.RatingCount;
                    _myRatings[climb.ShareCode] = stars;
                    SaveMyRatings();
                    _logger.Info($"Rated climb {climb.ShareCode} with {stars} stars (avg: {data.RatingAvg:0.0}, votes: {data.RatingCount})");
                    callback?.Invoke(true, data.RatingAvg, data.RatingCount);
                }
                else
                {
                    _logger.Warning($"Rating submission failed for climb {climb.ShareCode}: {error}");
                    callback?.Invoke(false, 0f, 0);
                }
            });
        }

        // Submits a community cheat report. Callback: (success, communityFlagged)
        public void SubmitReport(ClimbData climb, string reason, Action<bool, bool> callback)
        {
            if (climb == null || !climb.IsFromCloud)
            {
                _logger.Warning("Report rejected: only cloud climbs can be reported");
                callback?.Invoke(false, false);
                return;
            }

            _apiService.SubmitReport(climb.Id.ToString(), VoterId, reason ?? "", (success, data, error) =>
            {
                if (success && data != null)
                {
                    climb.ReportCount = data.ReportCount;
                    climb.IsCommunityFlagged = data.CommunityFlagged;
                    _logger.Info($"Reported climb {climb.ShareCode} (total reports: {data.ReportCount}, community flagged: {data.CommunityFlagged})");
                    callback?.Invoke(true, data.CommunityFlagged);
                }
                else
                {
                    _logger.Warning($"Report submission failed for climb {climb.ShareCode}: {error}");
                    callback?.Invoke(false, false);
                }
            });
        }

        private void LoadMyRatings()
        {
            try
            {
                if (File.Exists(_myRatingsFilePath))
                {
                    string json = File.ReadAllText(_myRatingsFilePath);
                    _myRatings = JsonConvert.DeserializeObject<Dictionary<string, int>>(json, CommonJsonSettings.Default) ?? new Dictionary<string, int>();
                    _logger.Info($"Loaded {_myRatings.Count} own ratings from cache");
                }
            }
            catch (Exception e)
            {
                _logger.Error($"Failed to load own ratings: {e.Message}");
                _myRatings = new Dictionary<string, int>();
            }
        }

        private void SaveMyRatings()
        {
            // Defensive copy for background serialization
            var copy = new Dictionary<string, int>(_myRatings);
            FileUtils.WriteJsonFileInBackground(_logger, _myRatingsFilePath, copy);
        }
    }
}

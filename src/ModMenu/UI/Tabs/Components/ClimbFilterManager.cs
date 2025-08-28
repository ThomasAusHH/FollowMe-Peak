using System.Collections.Generic;
using System.Linq;
using FollowMePeak.Models;

namespace FollowMePeak.ModMenu.UI.Tabs.Components
{
    public class ClimbFilterManager
    {
        public enum BiomeFilter
        {
            All,
            Beach,
            Tropics,
            AlpineMesa,
            Caldera
        }
        
        private BiomeFilter _currentFilter = BiomeFilter.All;
        
        public BiomeFilter CurrentFilter => _currentFilter;
        
        public void SetFilter(BiomeFilter filter)
        {
            _currentFilter = filter;
        }
        
        public List<ClimbData> FilterClimbs(List<ClimbData> allClimbs)
        {
            if (_currentFilter == BiomeFilter.All)
                return allClimbs;
            
            return allClimbs.Where(climb => MatchesBiomeFilter(climb.BiomeName)).ToList();
        }
        
        private bool MatchesBiomeFilter(string biomeName)
        {
            if (string.IsNullOrEmpty(biomeName)) return false;
            
            string normalizedBiome = biomeName.Replace(" ", "").ToLower();
            
            switch (_currentFilter)
            {
                case BiomeFilter.Beach:
                    return normalizedBiome.Contains("beach");
                    
                case BiomeFilter.Tropics:
                    return normalizedBiome.Contains("tropic") || 
                           normalizedBiome.Contains("jungle");
                    
                case BiomeFilter.AlpineMesa:
                    return normalizedBiome.Contains("alpine") || 
                           normalizedBiome.Contains("mesa") || 
                           normalizedBiome.Contains("mountain");
                    
                case BiomeFilter.Caldera:
                    return normalizedBiome.Contains("caldera") || 
                           normalizedBiome.Contains("volcano") || 
                           normalizedBiome.Contains("summit");
                    
                default:
                    return true;
            }
        }
    }
}
using System;
using System.Collections.Generic;
using Core.Requirements;
using UnityEngine;

namespace Managers.Requirements
{
    public class RequirementManager : MonoBehaviour
    {
        [SerializeField] private TextAsset _requirementJson;
        private RequirementCatalog _catalog;

        private void Awake()
        {
            var trailsByTheme = RequirementLoader.LoadFromJson(_requirementJson.text);

            _catalog = new RequirementCatalog(trailsByTheme);
        }

        public IReadOnlyList<Requirement> GetTrailForMatch(string theme, string playerOneId, string playerTwoId)
        {
            return _catalog.SelectTrailForMatch(theme, playerOneId, playerTwoId);
        }
    }
}
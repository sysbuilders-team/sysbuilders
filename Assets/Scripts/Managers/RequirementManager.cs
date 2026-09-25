using System;
using System.Collections.Generic;
using Core.Requirements;
using UnityEngine;

namespace Managers
{
    public class RequirementManager : MonoBehaviour, IRequirementProvider
    {
        [SerializeField] private TextAsset _requirementJson;
        private RequirementCatalog _catalog;

        public RequirementCatalog Catalog => _catalog;
        public string CurrentTheme { get; private set; }
        public string CurrentTrailId { get; private set; }
        public IReadOnlyList<Requirement> CurrentRequirements { get; private set; }

        private void Awake()
        {
            InitializeCatalog();
        }

        private void InitializeCatalog()
        {
            if (_catalog != null) return;

            if (_requirementJson == null)
            {
                // faltou arrastar a referencia no inspector 
                Debug.LogError("[RequirementManager] Arquivo JSON de requisitos não configurado no Inspector.", this);
                return;
            }

            var trailsByTheme = RequirementLoader.LoadFromJson(_requirementJson.text);
            _catalog = new RequirementCatalog(trailsByTheme);
        }

        public string GetRandomTrailId(string theme)
        {
            EnsureCatalogLoaded();
            return _catalog.SelectTrail(theme).Id;
        }

        public IReadOnlyList<Requirement> GetTrailById(string theme, string trailId)
        {
            EnsureCatalogLoaded();
            return _catalog.GetRequirementsForMatch(theme, trailId);
        }

        public IReadOnlyList<Requirement> SetActiveTrail(string theme, string trailId)
        {
            EnsureCatalogLoaded();
            CurrentTheme = theme;
            CurrentTrailId = trailId;
            CurrentRequirements = _catalog.GetRequirementsForMatch(theme, trailId);
            return CurrentRequirements;
        }

        public Trail SelectTrail(string theme)
        {
            EnsureCatalogLoaded();
            return _catalog.SelectTrail(theme);
        }

        public IReadOnlyList<Requirement> GetRequirementsForMatch(string theme, string trailId)
        {
            EnsureCatalogLoaded();
            return _catalog.GetRequirementsForMatch(theme, trailId);
        }

        Trail IRequirementProvider.GetTrailById(string theme, string trailId)
        {
            EnsureCatalogLoaded();
            return _catalog.GetTrailById(theme, trailId);
        }
        
        private void EnsureCatalogLoaded()
        {
            if (_catalog == null)
            {
                InitializeCatalog();
            }

            if (_catalog == null)
            {
                throw new InvalidOperationException("[RequirementManager] Catálogo de requisitos não foi inicializado.");
            }
        }
    }
}
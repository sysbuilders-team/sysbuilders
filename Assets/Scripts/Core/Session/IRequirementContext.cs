using System;
using System.Collections.Generic;
using Core.Requirements;

namespace Core.Session
{
    // Interface responsavel por exibir requisitos para UI
    // implementado pelo matchsession
    public interface IRequirementContext
    {
        Requirement CurrentRequirement { get; }
        IReadOnlyList<Requirement> RequirementsHistory { get; }

        // Passando o novo requirement como payload do evento
        event Action<Requirement> OnRequirementChanged; 
    }
}

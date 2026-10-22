using System;
using System.Linq;
using Core.Diagram;

namespace Core.Session
{
    // Func guarda a funcao que a classe que usa essa(matchsession) passa pra ela e entao,
    // temos como ver sempre que mudam os valores, quem decide quando executa eh essa classe
    // nao mais o chamador, o chamador so passa o metodo
    public class RefactorHandler : IRefactorContext
    {
        private readonly Func<PlayerState> _localPlayerProvider;
        private readonly Func<bool> _canModifyProvider;
        private readonly Func<ClassNode> _selectedClassProvider;
        private readonly Action _onClassRefactored;
        private readonly IRefactorPointPolicy _pointPolicy;

        private PlayerState LocalPlayer => _localPlayerProvider();
        private bool CanModifyDiagram => _canModifyProvider();

        public int RefactorPoints => LocalPlayer?.RefactorPoints ?? 0;
        public int RefactorCost => LocalPlayer?.RefactorCost ?? 1;

        public event Action<int> OnRefactorPointsChanged;

        public RefactorHandler(
            Func<PlayerState> localPlayerProvider,
            Func<bool> canModifyProvider,
            Func<ClassNode> selectedClassProvider = null,
            Action onClassRefactored = null,
            IRefactorPointPolicy pointPolicy = null)
        {
            _localPlayerProvider = localPlayerProvider ?? throw new ArgumentNullException(nameof(localPlayerProvider));
            _canModifyProvider = canModifyProvider ?? throw new ArgumentNullException(nameof(canModifyProvider));
            _selectedClassProvider = selectedClassProvider;
            _onClassRefactored = onClassRefactored;
            _pointPolicy = pointPolicy ?? new DefaultRefactorPointPolicy();
        }

        public void GrantInitialPoints(PlayerState playerOne, PlayerState playerTwo)
        {
            int initialPoints = _pointPolicy.InitialPoints;
            playerOne?.AddRefactorPoints(initialPoints);
            playerTwo?.AddRefactorPoints(initialPoints);
        }

        public void GrantTurnPoints(int turnNumber, PlayerState playerOne, PlayerState playerTwo)
        {
            if (turnNumber <= 1) return;

            int points = _pointPolicy.GetPointsForTurn(turnNumber);
            if (points <= 0) return;

            playerOne?.AddRefactorPoints(points);
            playerTwo?.AddRefactorPoints(points);
            OnRefactorPointsChanged?.Invoke(RefactorPoints);
        }

        public bool CanRefactor(ClassNode classNode)
        {
            if (!CanModifyDiagram || classNode == null || !classNode.IsLocked)
                return false;

            var player = LocalPlayer;
            if (player == null || !player.Diagram.Classes.Contains(classNode))
                return false;

            return player.RefactorPoints >= player.RefactorCost;
        }

        public bool CanRefactorSelected()
        {
            var selected = _selectedClassProvider?.Invoke();
            return selected != null && CanRefactor(selected);
        }

        public bool TryRefactorSelectedClass()
        {
            var selected = _selectedClassProvider?.Invoke();
            if (selected == null) return false;
            return TryRefactorClass(selected);
        }

        public bool TryRefactorClass(ClassNode classNode)
        {
            if (!CanRefactor(classNode)) return false;

            var player = LocalPlayer;
            if (!player.TryDeductPoints(player.RefactorCost))
                return false;

            classNode.Unlock();
            OnRefactorPointsChanged?.Invoke(player.RefactorPoints);
            _onClassRefactored?.Invoke();
            return true;
        }
    }
}

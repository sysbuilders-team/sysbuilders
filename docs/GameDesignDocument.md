SysBuilders — Game Design Document (One-Pager)
# A Premissa
SysBuilders é um jogo educacional 1v1 desenvolvido em Unity, focado em ensinar conceitos práticos de Análise e Projeto de Sistemas (Orientação a Objetos) para estudantes de Ciência da Computação e Sistemas de Informação.
Os jogadores assumem o papel de arquitetos de software competindo para construir a arquitetura mais limpa, coesa e adaptável diante de requisitos de sistema que mudam a cada turno.

# Core Loop (O Ciclo de Jogo)
O jogo acontece em turnos assíncronos e simultâneos (Jogador A e Jogador B jogam seus turnos de forma independente). Cada turno segue este ciclo:

Novos Requisitos: O jogo apresenta uma nova funcionalidade que o sistema deve suportar e uma lista de novos métodos disponíveis.

Construção: O jogador atualiza seu diagrama de classes. Ele pode criar novas classes, alocar métodos, definir relações (Associação) e refatorar classes antigas.

Bloqueio (Lock): Ao finalizar o turno, as classes modificadas são "congeladas".

Avaliação: O motor do jogo avalia a arquitetura criada com base em métricas de Engenharia de Software. O ciclo se repete até o fim da partida.

## Mecânicas Principais (Fase 1)
Diagramação Livre: Os jogadores nomeiam suas próprias classes livremente (nomes não afetam a pontuação).

Alocação de Métodos: Métodos possuem uma "Categoria" invisível (ex: Usuario, Emprestimo). O jogador deve deduzir a categoria pelo nome do método para agrupá-los corretamente, maximizando a coesão.

Gestão de Relações: Conexões entre classes (no momento, apenas Associação). O sistema lê o diagrama completo, não apenas classes isoladas.

Custo de Refatoração: Modificar uma classe que já foi "bloqueada" (Locked) em um turno anterior custa Pontos de Refatoração.

Sistema de Avaliação (O Árbitro): A pontuação não busca uma "resposta certa" única. O sistema recompensa arquiteturas preparadas para o futuro e penaliza Code Smells específicos:

Large Class (Classes muito grandes).

Duplicate Code (Lógica redundante).

Baixa Coesão (Métodos de categorias diferentes na mesma classe).

## O que o jogo NÃO é (Fora de Escopo / Fase 2)
Para garantir a entrega da Fase 1, as seguintes ideias estão temporariamente vetadas e não devem ser implementadas agora:

Sem Interação em Tempo Real: O multiplayer é estritamente baseado em sincronização de fim de turno ("Jogador A finalizou -> Jogador B finalizou -> Avança"). Não há visualização do cursor do oponente ou chat em tempo real.

Sem Padrões de Projeto Complexos: A avaliação foca no básico bem feito (Coesão e Acoplamento). Padrões como Observer ou Strategy ficam para depois.

Sem Sabotagem: Habilidades de "Espionar" ou "Injetar Code Smell" no diagrama do adversário pertencem à Fase 2.

Sem Relações Avançadas: Herança, Composição e Agregação não fazem parte do MVP (embora o código já deva usar um enum para facilitar a adição delas no futuro).

## Objetivos da Fase 2 a serem definidos futuramente
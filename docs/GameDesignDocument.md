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
 
Sem Atributos: O jogador trabalha apenas com métodos. Não existem cards de atributo (nem livres, nem fornecidos pelo sistema, nem derivados automaticamente dos métodos) na Fase 1. Essa decisão foi avaliada explicitamente — inclusive a alternativa de atributos derivados automaticamente dos métodos alocados — e descartada para manter o foco do jogador em decisões de coesão e agrupamento, sem carga adicional de interface.
 
Gestão de Relações: Conexões entre classes (no momento, apenas Associação). O sistema lê o diagrama completo, não apenas classes isoladas.
 
Custo de Refatoração: Modificar uma classe que já foi "bloqueada" (Locked) em um turno anterior custa Pontos de Refatoração.
 
Sistema de Avaliação (O Árbitro): A pontuação não busca uma "resposta certa" única. O sistema recompensa arquiteturas preparadas para o futuro e penaliza Code Smells específicos:
 
Large Class (Classes muito grandes).
 
Duplicate Code (Lógica redundante).
 
Baixa Coesão (Métodos de categorias diferentes na mesma classe).
 
## A Fonte da Dificuldade: Design das Trilhas de Requisitos
Como a Fase 1 usa apenas Associação (sem Herança, sem atributos), a profundidade estratégica do jogo depende inteiramente de como as trilhas de requisitos são escritas. Cada trilha (sequência fixa de 5 requisitos por tema) deve ser desenhada com armadilhas intencionais, por exemplo:
 
Falsa Coesão: Entregar métodos que parecem pertencer ao mesmo tema mas têm responsabilidades diferentes, punindo agrupamento por preguiça.
 
Regra Fragmentada / Inveja de Recurso: Fazer com que métodos de turnos diferentes tratem da mesma regra de negócio em classes distintas, forçando o jogador a decidir entre gastar Pontos de Refatoração para unificar ou aceitar a penalidade de coesão.
 
Esse tipo de armadilha (validado com exemplos concretos durante o design) já gera decisões de arquitetura sob pressão sem precisar de nenhum tipo de relação além de Associação. A responsabilidade de garantir que o jogo seja desafiador está no conteúdo das trilhas, não em mecânicas adicionais.
 
## Hipóteses em Aberto (a validar via playtest, não regras fechadas)
Tempo por Turno: Foi cogitado um limite de ~2 minutos por turno, com a expectativa de que a pressão cresça nos turnos finais conforme o volume de métodos aumenta (turno 1 com poucos métodos, turno 5 podendo chegar a ~20). Esse número não está fechado — precisa ser validado jogando os turnos finais com volume real de métodos antes de virar regra definitiva.
 
## O que o jogo NÃO é (Fora de Escopo / Fase 2)
Para garantir a entrega da Fase 1, as seguintes ideias estão temporariamente vetadas e não devem ser implementadas agora:
 
Sem Interação em Tempo Real: O multiplayer é estritamente baseado em sincronização de fim de turno ("Jogador A finalizou -> Jogador B finalizou -> Avança"). Não há visualização do cursor do oponente ou chat em tempo real.
 
Sem Padrões de Projeto Complexos: A avaliação foca no básico bem feito (Coesão e Acoplamento). Padrões como Observer ou Strategy ficam para depois.
 
Sem Sabotagem: Habilidades de "Espionar" ou "Injetar Code Smell" no diagrama do adversário pertencem à Fase 2.
 
Sem Relações Avançadas: Herança, Composição e Agregação não fazem parte do MVP (embora o código já deva usar um enum para facilitar a adição delas no futuro). Essa exclusão foi reavaliada e reafirmada: os exemplos de dificuldade que motivaram a discussão sobre Herança (armadilhas de coesão e "inveja de recurso") são alcançáveis só com Associação, então Herança — junto com a detecção de violação de Liskov e novos code smells associados a ela (ex: Lazy Class, Over-engineering) — fica para a Fase 2.
 
Sem Atributos: Reafirmado como fora de escopo da Fase 1 (ver seção de Mecânicas Principais acima).
 
## Objetivos da Fase 2 a serem definidos futuramente
Candidatos já identificados durante o design da Fase 1: Herança (com detecção de violação de Liskov), Composição, Agregação, e novos Code Smells associados a essas relações (Lazy Class, Over-engineering).

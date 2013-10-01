using System;
using System.Collections.Generic;
using System.Text;

namespace SrcChess {
    /// <summary>Base class for Search Engine</summary>
    public class SearchEngineAlphaBeta : SearchEngine {

        /// <summary>Delegates which can be used to called the FindBestMoveUsingAlphaBeta method asynchronously</summary>
        private delegate int                FindBestMoveUsingAlphaBetaAsyncDel(ChessBoard chessBoard, SearchMode searchMode, ChessBoard.PlayerColorE ePlayerColor, int iThreadId, List<ChessBoard.MovePosS> moveList, int iTotalMoveCount, int iAlpha, int iBeta, out int iPermCount, out ChessBoard.MovePosS movePosBest, out int iMaxDepth);

        /// <summary>Private class use to pass info at AlphaBeta decreasing the stack space use</summary>
        private class AlphaBetaInfo {
            /// <summary>Transposition table</summary>
            public TransTable               m_transTable;
            /// <summary>Time before timeout. Use for iterative</summary>
            public DateTime                 m_dtTimeOut;
            /// <summary>Number of board evaluated</summary>
            public int                      m_iPermCount;
            /// <summary>Array of move position per depth</summary>
            public ChessBoard.MovePosS[]    m_arrMovePos;
            /// <summary>Maximum depth to search</summary>
            public int                      m_iMaxDepth;
            /// <summary>Search mode</summary>
            public SearchMode               m_searchMode;
            /// <summary>Number of squares attacked by pieces</summary>
            public int                      m_iAttackedPos;
            /// <summary>Number of enemy pieces attacked by pieces</summary>
            public int                      m_iAttackedPieces;
        };

        //*********************************************************     
        //
        /// <summary>
        /// Class constructor
        /// </summary>
        /// <param name="trace">    Trace object or null</param>
        /// <param name="rnd">      Random object</param>
        /// <param name="rndRep">   Repetitive random object</param>
        //  
        //*********************************************************     
        public SearchEngineAlphaBeta(ITrace trace, Random rnd, Random rndRep) : base(trace, rnd, rndRep) {
        }

        //*********************************************************     
        //
        /// <summary>
        /// Alpha Beta pruning function.
        /// </summary>
        /// <param name="chessBoard">       Chess board</param>
        /// <param name="ePlayerColor">     Color doing the move</param>
        /// <param name="iDepth">           Actual search depth</param>
        /// <param name="iAlpha">           Alpha limit</param>
        /// <param name="iBeta">            Beta limit</param>
        /// <param name="iWhiteMoveCount">  Number of moves white can do</param>
        /// <param name="iBlackMoveCount">  Number of moves black can do</param>
        /// <param name="abInfo">           Supplemental information</param>
        /// <returns>
        /// Points to give for this move or Int32.MinValue for timed out
        /// </returns>
        //  
        //*********************************************************     
        private int AlphaBeta(ChessBoard chessBoard, ChessBoard.PlayerColorE ePlayerColor, int iDepth, int iAlpha, int iBeta, int iWhiteMoveCount, int iBlackMoveCount, AlphaBetaInfo abInfo) {
            int                         iRetVal;
            List<ChessBoard.MovePosS>   moveList;
            int                         iPts;
            int                         iMoveCount;
            int                         iAttackedPos;
            int                         iAttackedPieces;
            TransEntryTypeE             eType = TransEntryTypeE.Alpha;
            ChessBoard.BoardStateMaskE  eBoardExtraInfo;
            ChessBoard.RepeatResultE    eResult;

            if (abInfo.m_dtTimeOut != DateTime.MaxValue && DateTime.Now >= abInfo.m_dtTimeOut) {
                iRetVal = Int32.MinValue;   // Time out!
            } else if (chessBoard.IsEnoughPieceForCheckMate()) {
                eBoardExtraInfo = chessBoard.ComputeBoardExtraInfo(ePlayerColor, true);
                iRetVal         = (abInfo.m_transTable != null) ? abInfo.m_transTable.ProbeEntry(chessBoard.CurrentZobristKey, eBoardExtraInfo, iDepth, iAlpha, iBeta) : Int32.MaxValue;
                if (iRetVal == Int32.MaxValue) {
                    if (iDepth == 0 || m_bCancelSearch) {
                        if (ePlayerColor == ChessBoard.PlayerColorE.White) {
                            iAttackedPos    = abInfo.m_iAttackedPos;
                            iAttackedPieces = abInfo.m_iAttackedPieces;
                        } else {
                            iAttackedPos    = -abInfo.m_iAttackedPos;
                            iAttackedPieces = -abInfo.m_iAttackedPieces;
                        }
                        iRetVal = (ePlayerColor == ChessBoard.PlayerColorE.Black) ? -chessBoard.Points(abInfo.m_searchMode, ePlayerColor, iWhiteMoveCount - iBlackMoveCount, iAttackedPos, iAttackedPieces) : 
                                                                                     chessBoard.Points(abInfo.m_searchMode, ePlayerColor, iWhiteMoveCount - iBlackMoveCount, iAttackedPos, iAttackedPieces);
                        abInfo.m_iPermCount++;
                        if (abInfo.m_transTable != null) {
                            abInfo.m_transTable.RecordEntry(chessBoard.CurrentZobristKey, eBoardExtraInfo, iDepth, iRetVal, TransEntryTypeE.Exact);
                        }
                    } else {
                        moveList    = chessBoard.EnumMoveList(ePlayerColor, true, out abInfo.m_iAttackedPos, out abInfo.m_iAttackedPieces);
                        iMoveCount  = moveList.Count;
                        if (ePlayerColor == ChessBoard.PlayerColorE.White) {
                            iWhiteMoveCount = iMoveCount;
                        } else {
                            iBlackMoveCount = iMoveCount;
                        }
                        if (iMoveCount == 0) {
                            if (chessBoard.IsCheck(ePlayerColor)) {
                                iRetVal = -1000000 - iDepth;
                            } else {
                                iRetVal = 0;    // Draw
                            }
                            if (abInfo.m_transTable != null) {
                                abInfo.m_transTable.RecordEntry(chessBoard.CurrentZobristKey, eBoardExtraInfo, iDepth, iRetVal, TransEntryTypeE.Exact);
                            }
                        } else {
                            iRetVal = iAlpha;
                            foreach (ChessBoard.MovePosS move in moveList) {
                                eResult = chessBoard.DoMoveNoLog(move);
                                abInfo.m_arrMovePos[iDepth - 1] = move;
                                if (eResult == ChessBoard.RepeatResultE.NoRepeat) {
                                    iPts = -AlphaBeta(chessBoard,
                                                      (ePlayerColor == ChessBoard.PlayerColorE.Black) ? ChessBoard.PlayerColorE.White : ChessBoard.PlayerColorE.Black,
                                                      iDepth - 1,
                                                      -iBeta,
                                                      -iRetVal,
                                                      iWhiteMoveCount,
                                                      iBlackMoveCount,
                                                      abInfo);
                                } else {
                                    iPts = 0;
                                }
                                chessBoard.UndoMoveNoLog(move);
                                if (iPts == Int32.MinValue) {
                                    iRetVal = iPts;
                                    break;
                                } else {
                                    if (iPts > iRetVal) {
                                        iRetVal = iPts;
                                        eType   = TransEntryTypeE.Exact;
                                    }
                                    if (iRetVal >= iBeta) {
                                        iRetVal = iBeta;
                                        eType   = TransEntryTypeE.Beta;
                                        break;
                                    }
                                }
                            }
                            if (abInfo.m_transTable != null && iRetVal != Int32.MinValue) {
                                abInfo.m_transTable.RecordEntry(chessBoard.CurrentZobristKey, eBoardExtraInfo, iDepth, iRetVal, eType);
                            }
                        }
                    }
                }
            } else {
                iRetVal = 0;
            }
            return(iRetVal);
        }

        //*********************************************************     
        //
        /// <summary>
        /// Find the best move for a player using alpha-beta for a given depth
        /// </summary>
        /// <param name="chessBoard">       Chess board</param>
        /// <param name="searchMode">       Search mode</param>
        /// <param name="ePlayerColor">     Color doing the move</param>
        /// <param name="moveList">         List of move to try</param>
        /// <param name="iTotalMoveCount">  Total list of moves</param>
        /// <param name="iDepth">           Maximum depth</param>
        /// <param name="iAlpha">           Alpha bound</param>
        /// <param name="iBeta">            Beta bound</param>
        /// <param name="transTable">       Transposition table or null if not using one</param>
        /// <param name="dtTimeOut">        Time limit (DateTime.MaxValue for no time limit)</param>
        /// <param name="iPermCount">       Total permutation evaluated</param>
        /// <param name="iBestMoveIndex">   Index of the best move</param>
        /// <param name="bTimeOut">         Return true if time out</param>
        /// <param name="arrPoints">        Returns point of each move in move list</param>
        /// <returns>
        /// Points
        /// </returns>
        //  
        //*********************************************************     
        private int FindBestMoveUsingAlphaBetaAtDepth(ChessBoard chessBoard, SearchMode searchMode, ChessBoard.PlayerColorE ePlayerColor, List<ChessBoard.MovePosS> moveList, int iTotalMoveCount, int iDepth, int iAlpha, int iBeta, TransTable transTable, DateTime dtTimeOut, out int iPermCount, out int iBestMoveIndex, out bool bTimeOut, out int[] arrPoints) {
            int                         iRetVal = -10000000;
            int                         iWhiteMoveCount;
            int                         iBlackMoveCount;
            int                         iMoveCount;
            int                         iIndex;
            int                         iPts;
            ChessBoard.MovePosS         move;
            AlphaBetaInfo               abInfo;
            ChessBoard.RepeatResultE    eResult;
                        
            bTimeOut                    = false;
            abInfo                      = new AlphaBetaInfo();
            abInfo.m_arrMovePos         = new ChessBoard.MovePosS[iDepth];
            abInfo.m_iPermCount         = 0;
            abInfo.m_dtTimeOut          = dtTimeOut;
            abInfo.m_transTable         = transTable;
            abInfo.m_iMaxDepth          = iDepth;
            abInfo.m_searchMode         = searchMode;
            abInfo.m_iAttackedPos       = 0;
            abInfo.m_iAttackedPieces    = 0;
            iBestMoveIndex              = -1;
            arrPoints                   = new int[moveList.Count];
            if (ePlayerColor == ChessBoard.PlayerColorE.White) {
                iWhiteMoveCount = iTotalMoveCount;
                iBlackMoveCount = 0;
            } else {
                iWhiteMoveCount = 0;
                iBlackMoveCount = iTotalMoveCount;
            }
            iMoveCount = moveList.Count;
            iIndex     = 0;
            iRetVal    = iAlpha;
            while (iIndex < iMoveCount && !bTimeOut) {
                move                            = moveList[iIndex];
                eResult                         = chessBoard.DoMoveNoLog(move);
                abInfo.m_arrMovePos[iDepth - 1] = move;
                if (eResult == ChessBoard.RepeatResultE.NoRepeat) {
                    iPts = -AlphaBeta(chessBoard,
                                      (ePlayerColor == ChessBoard.PlayerColorE.Black) ? ChessBoard.PlayerColorE.White : ChessBoard.PlayerColorE.Black,
                                      iDepth - 1,
                                      -iBeta,
                                      -iRetVal,
                                      iWhiteMoveCount,
                                      iBlackMoveCount,
                                      abInfo);
                } else {
                    iPts = 0;
                }                                         
                arrPoints[iIndex] = iPts;
                chessBoard.UndoMoveNoLog(move);
                if (iPts == Int32.MinValue) {
                    iRetVal  = iPts;
                    bTimeOut = true;
                } else {
                    if (iPts > iRetVal) {
                        TraceSearch(iDepth, ePlayerColor, move, iPts);
                        iRetVal         = iPts;
                        iBestMoveIndex  = iIndex;
                    }
                }
                iIndex++;
            }
            iPermCount = abInfo.m_iPermCount;
            return(iRetVal);
        }

        //*********************************************************     
        //
        /// <summary>
        /// Find the best move for a player using alpha-beta in a secondary thread
        /// </summary>
        /// <param name="chessBoard">       Chess board</param>
        /// <param name="searchMode">       Search mode</param>
        /// <param name="ePlayerColor">     Color doing the move</param>
        /// <param name="iThreadId">        Thread Id (0-n)</param>
        /// <param name="moveList">         List of move to try</param>
        /// <param name="iTotalMoveCount">  Total number of moves</param>
        /// <param name="iAlpha">           Alpha bound</param>
        /// <param name="iBeta">            Beta bound</param>
        /// <param name="iPermCount">       Total permutation evaluated</param>
        /// <param name="movePosBest">      Best move</param>
        /// <param name="iMaxDepth">        Maximum depth evaluated</param>
        /// <returns>
        /// Points
        /// </returns>
        //  
        //*********************************************************     
        private int FindBestMoveUsingAlphaBetaAsync(ChessBoard chessBoard, SearchMode searchMode, ChessBoard.PlayerColorE ePlayerColor, int iThreadId, List<ChessBoard.MovePosS> moveList, int iTotalMoveCount, int iAlpha, int iBeta, out int iPermCount, out ChessBoard.MovePosS movePosBest, out int iMaxDepth) {
            int                             iRetVal;
            DateTime                        dtTimeOut;
            int                             iDepth;
            int                             iPermCountAtLevel;
            int                             iPoint;
            int                             iBestMoveIndex;
            int                             iDepthLimit;
            int[]                           arrPoints;
            System.Threading.ThreadPriority eThreadPriority;
            TransTable                      transTable;
            bool                            bTimeOut;
            bool                            bIterativeDepthFirst;
            
            eThreadPriority                                = System.Threading.Thread.CurrentThread.Priority;
            System.Threading.Thread.CurrentThread.Priority = System.Threading.ThreadPriority.BelowNormal;
            if ((searchMode.m_eOption & SearchMode.OptionE.UseTransTable) != 0) {
                transTable = TransTable.GetTransTable(iThreadId);
                transTable.Reset();
            } else {
                transTable = null;
            }
            bIterativeDepthFirst        = ((searchMode.m_eOption & SearchMode.OptionE.UseIterativeDepthSearch) == SearchMode.OptionE.UseIterativeDepthSearch);
            movePosBest.StartPos        = 255;
            movePosBest.EndPos          = 255;
            movePosBest.OriginalPiece   = ChessBoard.PieceE.None;
            movePosBest.Type            = ChessBoard.MoveTypeE.Normal;
            try {
                iPermCount  = 0;
                if (searchMode.m_iSearchDepth == 0 || bIterativeDepthFirst) {
                    dtTimeOut   = (bIterativeDepthFirst) ? DateTime.MaxValue : DateTime.Now + TimeSpan.FromSeconds(searchMode.m_iTimeOutInSec);
                    iDepthLimit = (bIterativeDepthFirst) ? searchMode.m_iSearchDepth : 999;
                    iDepth      = 1;
                    iRetVal     = FindBestMoveUsingAlphaBetaAtDepth(chessBoard, searchMode, ePlayerColor, moveList, iTotalMoveCount, iDepth, iAlpha, iBeta, transTable, DateTime.MaxValue, out iPermCountAtLevel, out iBestMoveIndex, out bTimeOut, out arrPoints);
                    if (iBestMoveIndex != -1) {
                        movePosBest = moveList[iBestMoveIndex];
                    }
                    iPermCount += iPermCountAtLevel;
                    iMaxDepth   = iDepth;
                    while (DateTime.Now < dtTimeOut && !m_bCancelSearch && !bTimeOut && iDepth < iDepthLimit) {
                        moveList = SortMoveList(moveList, arrPoints);
                        iDepth++;
                        iPoint  = FindBestMoveUsingAlphaBetaAtDepth(chessBoard, searchMode, ePlayerColor, moveList, iTotalMoveCount, iDepth, iAlpha, iBeta, transTable, dtTimeOut, out iPermCountAtLevel, out iBestMoveIndex, out bTimeOut, out arrPoints);
                        if (!bTimeOut) {
                            if (iBestMoveIndex != -1) {
                                movePosBest = moveList[iBestMoveIndex];
                            }
                            iPermCount    += iPermCountAtLevel;
                            iMaxDepth      = iDepth;
                            iRetVal        = iPoint;
                        }
                    } 
                } else {
                    iMaxDepth = searchMode.m_iSearchDepth;
                    iRetVal   = FindBestMoveUsingAlphaBetaAtDepth(chessBoard, searchMode, ePlayerColor, moveList, iTotalMoveCount, iMaxDepth, iAlpha, iBeta, transTable, DateTime.MaxValue, out iPermCount, out iBestMoveIndex, out bTimeOut, out arrPoints);
                    if (iBestMoveIndex != -1) {
                        movePosBest = moveList[iBestMoveIndex];
                    }
                }
            } finally {
                System.Threading.Thread.CurrentThread.Priority = eThreadPriority;
            }
            return(iRetVal);
        }

        //*********************************************************     
        //
        /// <summary>
        /// Find the best move for a player using alpha-beta
        /// </summary>
        /// <param name="chessBoard">       Chess board</param>
        /// <param name="searchMode">       Search mode</param>
        /// <param name="ePlayerColor">     Color doing the move</param>
        /// <param name="moveList">         Move list</param>
        /// <param name="arrIndex">         Order of evaluation of the moves</param>
        /// <param name="moveBest">         Best move found</param>
        /// <param name="iPermCount">       Total permutation evaluated</param>
        /// <param name="iCacheHit">        Number of moves found in the translation table cache</param>
        /// <param name="iMaxDepth">        Maximum depth to use</param>
        /// <returns>
        /// true if a move has been found
        /// </returns>
        //  
        //*********************************************************     
        protected override bool FindBestMove(ChessBoard                 chessBoard,
                                             SearchEngine.SearchMode    searchMode,
                                             ChessBoard.PlayerColorE    ePlayerColor,
                                             List<ChessBoard.MovePosS>  moveList,
                                             int[]                      arrIndex, 
                                             ref ChessBoard.MovePosS    moveBest,
                                             out int                    iPermCount,
                                             out int                    iCacheHit,
                                             out int                    iMaxDepth) {
            bool                                    bRetVal = false;
            bool                                    bMultipleThread;
            bool                                    bUseTransTable;
            ChessBoard[]                            arrChessBoard;
            FindBestMoveUsingAlphaBetaAsyncDel[]    arrDelFindBestMoveUsingAlphaBetaAsync;
            IAsyncResult[]                          arrAsyncResult;
            List<ChessBoard.MovePosS>[]             arrMoveList;
            ChessBoard.MovePosS                     movePosTmp;
            int                                     iPts;
            int                                     iAlpha;
            int                                     iBeta;
            int                                     iThreadCount;
            int                                     iMovePermCount;
            int                                     iMaxDepthTmp;
            
            //TODO Enable transposition table when bug on 3 repetition move draw will be found.
            searchMode.m_eOption       &= ~SearchMode.OptionE.UseTransTable;
            bUseTransTable              = ((searchMode.m_eOption & SearchMode.OptionE.UseTransTable) != 0);
            iCacheHit                   = 0;
            iMaxDepth                   = 0;
            iPermCount                  = 0;
            iAlpha                      = -10000000;
            iBeta                       = +10000000;
            bMultipleThread             = (searchMode.m_eThreadingMode == SearchMode.ThreadingModeE.OnePerProcessorForSearch);
            iThreadCount                = System.Environment.ProcessorCount;
            if (bMultipleThread && iThreadCount < 2) {
                bMultipleThread = false;    // No reason to go with multi-threading if only one processor
            }
            if (bMultipleThread) {
                arrChessBoard                           = new ChessBoard[iThreadCount];
                arrAsyncResult                          = new IAsyncResult[iThreadCount];
                arrDelFindBestMoveUsingAlphaBetaAsync   = new FindBestMoveUsingAlphaBetaAsyncDel[iThreadCount];
                arrMoveList                             = new List<ChessBoard.MovePosS>[iThreadCount];
                for (int iIndex = 0; iIndex < iThreadCount; iIndex++) {
                    arrChessBoard[iIndex]                           = chessBoard.Clone();
                    arrDelFindBestMoveUsingAlphaBetaAsync[iIndex]   = new FindBestMoveUsingAlphaBetaAsyncDel(FindBestMoveUsingAlphaBetaAsync);
                    arrMoveList[iIndex]                             = new List<ChessBoard.MovePosS>(moveList.Count / iThreadCount + 1);
                    for (int iStep = iIndex; iStep < moveList.Count; iStep += iThreadCount) {
                        arrMoveList[iIndex].Add(moveList[arrIndex[iStep]]);
                    }
                }
                for (int iStep = 0; iStep < iThreadCount; iStep++) {
                    arrAsyncResult[iStep]  = arrDelFindBestMoveUsingAlphaBetaAsync[iStep].BeginInvoke(arrChessBoard[iStep],
                                                                                                      searchMode,
                                                                                                      ePlayerColor,
                                                                                                      iStep,
                                                                                                      arrMoveList[iStep],
                                                                                                      moveList.Count,
                                                                                                      iAlpha,
                                                                                                      iBeta,
                                                                                                      out iMovePermCount,
                                                                                                      out movePosTmp,
                                                                                                      out iMaxDepth,
                                                                                                      null,
                                                                                                      null);
                }
                iMaxDepth = 999;
                for (int iStep = 0; iStep < iThreadCount; iStep++) {
                    iPts  = arrDelFindBestMoveUsingAlphaBetaAsync[iStep].EndInvoke(out iMovePermCount, out movePosTmp, out iMaxDepthTmp, arrAsyncResult[iStep]);
                    if (movePosTmp.StartPos != 255) {
                        iPermCount += iMovePermCount;
                        iMaxDepth   = Math.Min(iMaxDepth, iMaxDepthTmp);
                        if (bUseTransTable) {
                            iCacheHit  +=  TransTable.GetTransTable(iStep).CacheHit;
                        }
                        if (iPts > iAlpha) {
                            iAlpha      = iPts;
                            moveBest    = movePosTmp;
                            bRetVal     = true;
                        }
                    }
                }
                if (iMaxDepth == 999) {
                    iMaxDepth = -1;
                }
            } else {
                ChessBoard                  chessBoardTmp;
                List<ChessBoard.MovePosS>   moveListTmp;
                
                chessBoardTmp = chessBoard.Clone();
                moveListTmp   = new List<ChessBoard.MovePosS>(moveList.Count);
                for (int iIndex = 0; iIndex < moveList.Count; iIndex++) {
                    moveListTmp.Add(moveList[arrIndex[iIndex]]);
                }
                iPts  = FindBestMoveUsingAlphaBetaAsync(chessBoardTmp,
                                                        searchMode,
                                                        ePlayerColor,
                                                        0,  // ThreadId
                                                        moveListTmp,
                                                        moveList.Count,
                                                        iAlpha,
                                                        iBeta,
                                                        out iPermCount,
                                                        out movePosTmp,
                                                        out iMaxDepth);
                if (movePosTmp.StartPos != 255) {
                    if (bUseTransTable) {
                        iCacheHit  +=  TransTable.GetTransTable(0).CacheHit;
                    }
                    moveBest    = movePosTmp;
                    bRetVal     = true;
                }
            }
            return(bRetVal);
        }
    }
}

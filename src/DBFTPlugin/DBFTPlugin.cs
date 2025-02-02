// Copyright (C) 2015-2021 The Neo Project.
//
// The Neo.Consensus.DBFT is free software distributed under the MIT software license,
// see the accompanying file LICENSE in the main directory of the
// project or http://www.opensource.org/licenses/mit-license.php
// for more details.
//
// Redistribution and use in source and binary forms with or without
// modifications are permitted.

using Akka.Actor;
using Neo.ConsoleService;
using Neo.Network.P2P;
using Neo.Plugins;
using Neo.Wallets;

namespace Neo.Consensus
{
    public class DBFTPlugin : Plugin, IP2PPlugin
    {
        private IWalletProvider walletProvider;
        private IActorRef consensus;
        private bool started = false;
        private NeoSystem neoSystem;
        private Settings settings;

        public DBFTPlugin() { }

        public DBFTPlugin(Settings settings)
        {
            this.settings = settings;
        }

        public override string Description => "Consensus plugin with dBFT algorithm.";

        protected override void Configure()
        {
            settings ??= new Settings(GetConfiguration());
        }

        protected override void OnSystemLoaded(NeoSystem system)
        {
            if (system.Settings.Network != settings.Network) return;
            neoSystem = system;
            neoSystem.ServiceAdded += NeoSystem_ServiceAdded;
        }

        private void NeoSystem_ServiceAdded(object sender, object service)
        {
            if (service is not IWalletProvider provider) return;
            walletProvider = provider;
            neoSystem.ServiceAdded -= NeoSystem_ServiceAdded;
            if (settings.AutoStart)
            {
                walletProvider.WalletChanged += WalletProvider_WalletChanged;
            }
        }

        private void WalletProvider_WalletChanged(object sender, Wallet wallet)
        {
            walletProvider.WalletChanged -= WalletProvider_WalletChanged;
            Start(wallet);
        }

        [ConsoleCommand("start consensus", Category = "Consensus", Description = "Start consensus service (dBFT)")]
        private void OnStart()
        {
            Start(walletProvider.GetWallet());
        }

        public void Start(Wallet wallet)
        {
            if (started) return;
            started = true;
            consensus = neoSystem.ActorSystem.ActorOf(ConsensusService.Props(neoSystem, settings, wallet));
            consensus.Tell(new ConsensusService.Start());
        }

        bool IP2PPlugin.OnP2PMessage(NeoSystem system, Message message)
        {
            if (message.Command == MessageCommand.Transaction)
                consensus?.Tell(message.Payload);
            return true;
        }
    }
}

﻿using log4net;
using System;
using System.Collections.Generic;
using wServer.realm.entities.player;

namespace wServer.realm.commands
{
    public abstract class Command
    {
        protected static readonly ILog logger = LogManager.GetLogger(typeof(Command));

        public Command(string name, int requiredrank = 0)
        {
            CommandName = name;
            PermissionLevel = requiredrank;
        }

        public string CommandName { get; private set; }

        public int PermissionLevel { get; private set; }

        protected abstract bool Process(Player player, RealmTime time, string[] args);

        private static int GetPermissionLevel(Player player)
        {
            return player.Client.Account.Rank;
        }

        public bool HasPermission(Player player)
        {
            if (GetPermissionLevel(player) < PermissionLevel)
                return false;
            return true;
        }

        public bool Execute(Player player, RealmTime time, string args)
        {
            if (!HasPermission(player))
            {
                player.SendInfo("You are not an Admin");
                return false;
            }

            try
            {
                string[] a = args.Split(' ');
                bool success = Process(player, time, a);
                if (success)
                player.Manager.Database.DoActionAsync(db =>
                {
                    var cmd = db.CreateQuery();
                    cmd.CommandText = "insert into commandlog (command, args, player) values (@command, @args, @player);";
                    cmd.Parameters.AddWithValue("@command", CommandName);
                    cmd.Parameters.AddWithValue("@args", args);
                    cmd.Parameters.AddWithValue("@player", $"{player.AccountId}:{player.Name}");
                    cmd.ExecuteNonQuery();
                });
                return success;
            }
            catch (Exception ex)
            {
                logger.Error("Error when executing the command.", ex);
                player.SendError("Error when executing the command.");
                return false;
            }
        }
    }

    public class CommandManager
    {
        private static readonly ILog logger = LogManager.GetLogger(typeof(CommandManager));

        private readonly Dictionary<string, Command> cmds;

        private RealmManager manager;

        public CommandManager(RealmManager manager)
        {
            this.manager = manager;
            cmds = new Dictionary<string, Command>(StringComparer.InvariantCultureIgnoreCase);
            Type t = typeof(Command);
            foreach (Type i in t.Assembly.GetTypes())
                if (t.IsAssignableFrom(i) && i != t)
                {
                    Command instance = (Command)Activator.CreateInstance(i);
                    cmds.Add(instance.CommandName, instance);
                }
        }

        public IDictionary<string, Command> Commands
        {
            get { return cmds; }
        }

        public bool Execute(Player player, RealmTime time, string text)
        {
            int index = text.IndexOf(' ');
            string cmd = text.Substring(1, index == -1 ? text.Length - 1 : index - 1);
            string args = index == -1 ? "" : text.Substring(index + 1);

            Command command;
            if (!cmds.TryGetValue(cmd, out command))
            {
                player.SendError("Unknown command!");
                return false;
            }
            logger.InfoFormat("[Command] <{0}> {1}", player.Name, text);
            return command.Execute(player, time, args);
        }
    }
}

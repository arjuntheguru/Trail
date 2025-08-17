import React, { Fragment, useEffect } from 'react';
import { BrowserRouter as Router, Route, Switch } from 'react-router-dom';
import { useDispatch } from 'react-redux';
import { loadUser } from './store/auth-actions';


// import 'bootstrap/dist/css/bootstrap.min.css';
import Header from './components/Header/Header'

import './App.scss';
import Login from './containers/Account/Login';
import NotFound from './containers/Common/NotFound';
import SuperAdminRoute from './components/Common/SuperAdminRoute';
import AdminRoute from './components/Common/AdminRoute';
import CustomCalendarRoute from './components/Common/CustomCalendarRoute';
import CustomAdminRoute from './components/Common/CustomAdminRoute';
import Company from './containers/Company/Index';
import Task from './containers/Task/Index';
import Site from './containers/Site/Index';
import ManageTask from './containers/Site/ManageTask';
import ManageUser from './containers/Users/ManageUser';

import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

import '@inovua/reactdatagrid-community/base.css';
import '@inovua/reactdatagrid-community/theme/default-light.css';

import "react-datepicker/dist/react-datepicker.css";

import { Welcome } from './containers/Common/Welcome';
import CustomSuperAdminRoute from './components/Common/CustomSuperAdminRoute';
import ManageUser2 from './containers/Users/ManageUser2';
import Report from './containers/Report';


const App = () => {

  const dispatch = useDispatch();

  useEffect(() => {

    dispatch(loadUser())

  }, [dispatch])

  return (
    <Router>
      <Fragment>
        <Header />
        <ToastContainer />
        <main role="main" className="container-fluid pl-0 pr-0">
          <Switch>
            <Route exact strict path="/" component={Login} />
            <SuperAdminRoute exact strict path='/company' component={Company} />
            <CustomSuperAdminRoute exact strict path='/task' component={Task} />
            <CustomAdminRoute exact strict path='/site' component={Site} />
            <CustomCalendarRoute strict path='/site/:id' component={ManageTask} />
            <AdminRoute strict path='/users' component={ManageUser} />
            <CustomAdminRoute strict path='/users-manage' component={ManageUser} />
            <Route exact strict path="/welcome" component={Welcome} />
            <Route exact strict path="/overview" component={Report} />
            <Route path="*" component={NotFound} />
          </Switch>
        </main>
        <footer className="border-top fixed-bottom bg-dark text-center text-light p-2">
          <div className="container-fluid">
            &copy; {new Date().getFullYear()} - Dailo Technologies
          </div>
        </footer>
      </Fragment>
    </Router>
  );
}

export default App;

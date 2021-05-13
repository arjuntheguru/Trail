import React, { Fragment, useEffect } from 'react';
import { BrowserRouter as Router, Route, Switch } from 'react-router-dom';
import { useDispatch } from 'react-redux';
import { loadUser } from './store/auth-actions';

import 'bootstrap/dist/css/bootstrap.min.css';
import Header from './components/Header/Header'

import './App.css';
import Login from './containers/Account/Login';
import NotFound from './containers/Common/NotFound';
import SuperAdminRoute from './components/Common/SuperAdminRoute';
import AdminRoute from './components/Common/AdminRoute';
import Company from './containers/Company/Index';

import { ToastContainer } from 'react-toastify';
import 'react-toastify/dist/ReactToastify.css';

import '@inovua/reactdatagrid-community/base.css';
import '@inovua/reactdatagrid-community/theme/default-light.css';

import Site from './containers/Site/Index';
import { Welcome } from './containers/Common/Welcome';

const App = () => {

  const dispatch = useDispatch(); 

  useEffect(() => {

    dispatch(loadUser())

  }, [dispatch]) 

  return (
    <Router>
      <Fragment>
        <Header />
        <ToastContainer/> 
        <main role="main" className="container pb-3">
          <Switch>
            <Route exact strict path="/" component={Login} />
            <SuperAdminRoute exact strict path ='/company' component={Company}/>
            <AdminRoute exact strict path ='/site' component={Site}/>
            <Route exact strict path="/welcome" component={Welcome} />
            <Route path="*" component={NotFound} />
          </Switch>          
        </main>
        <footer className="border-top fixed-bottom bg-dark text-center text-light p-2">
        <div className="container">
            &copy; 2021 - Dailo Technologies
        </div>
    </footer>
      </Fragment>
    </Router>
  );
}

export default App;
